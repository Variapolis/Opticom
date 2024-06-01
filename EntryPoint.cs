using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;
using Rage;
using Rage.Attributes;
using Rage.Native;
using Object = Rage.Object;

[assembly: Rage.Attributes.Plugin("Opticom", Description = "Get to scenes quicker with no red lights!", Author = "Roheat")]
namespace NoMinimapOnFoot
{
    
    internal static class EntryPoint
    {
        internal static float SEARCH_STEP_SIZE = 10.0f;
        internal static float SEARCH_MIN_DISTANCE = 5.0f;
        internal static float SEARCH_MAX_DISTANCE = 40.0f;
        internal static float SEARCH_RADIUS = 10.0f;
        internal static float HEADING_THRESHOLD = 20.0f;
        internal static int TRAFFIC_LIGHT_POLL_FREQUENCY_MS = 50;
        internal static int TRAFFIC_LIGHT_GREEN_DURATION_MS = 5000;
        internal static Ped Player => Game.LocalPlayer.Character;

        internal static uint[] trafficLightObjects =
        {
            0x3e2b73a4,
            0x336e5e2a,
            0xd8eba922,
            0xd4729f50,
            0x272244b2,
            0x33986eae,
            0x2323cdc5
        };


        internal static void SetTrafficLightGreen(Object trafficLight)
        {
            GameFiber.StartNew(() =>
            {
                Vehicle[] nearbyVehs = Player.GetNearbyVehicles(16);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
                foreach (Vehicle v in nearbyVehs)
                {
                        if (v && v.Driver & v != Player.CurrentVehicle)
                        {
                            v.Driver.Tasks.PerformDrivingManeuver(v, VehicleManeuver.GoForwardStraightBraking, 4000);
                        }
                }
                GameFiber.Wait(TRAFFIC_LIGHT_GREEN_DURATION_MS);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
            });
        }

        internal static Vector3 TranslateVector3(Vector3 pos, float angle, float distance)
        {
            var angleRad = angle * 2.0 * Math.PI / 360.0;
            return new Vector3((float)(pos.X - distance * Math.Sin(angleRad)), (float)(pos.Y + distance * Math.Cos(angleRad)), pos.Z);
        }
        

        internal static void Main()
        {
            GameFiber.StartNew(() =>
            {
                while (true)
                {
                    GameFiber.Yield();
                    GameFiber.Wait(TRAFFIC_LIGHT_POLL_FREQUENCY_MS);
                    if (Player.IsInAnyVehicle(false) && Player.CurrentVehicle && Player.CurrentVehicle.IsSirenOn)
                    {
                        Vector3 position = Player.Position;
                        float heading = Player.Heading;
                        Object trafficLight = null;
                        for (float searchDistance = SEARCH_MAX_DISTANCE;
                             searchDistance > SEARCH_MIN_DISTANCE;
                             searchDistance -= SEARCH_STEP_SIZE)
                        {
                            Vector3 SearchPosition = TranslateVector3(position, heading, searchDistance);
                            foreach (uint tro in trafficLightObjects)
                            {
                                trafficLight = NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(SearchPosition,
                                    SEARCH_RADIUS, tro, false, false, false);

                                if (trafficLight != null)
                                {
                                    var lightHeading = trafficLight.Heading;
                                    var headingDiff = Math.Abs(heading - lightHeading);

                                    if (headingDiff < HEADING_THRESHOLD || headingDiff > (360 - HEADING_THRESHOLD))
                                    {
                                        SetTrafficLightGreen(trafficLight);
                                        break;
                                    }
                                    else
                                    {
                                        trafficLight = null;
                                    }
                                }
                            }
                        }

                    }
                }
            });
        }
    }
}
