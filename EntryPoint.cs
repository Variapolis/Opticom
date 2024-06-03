using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Attributes;
using Rage.Native;
using Object = Rage.Object;

namespace Opticom
{
    
    internal class EntryPoint : Plugin
    {
        internal static float SEARCH_STEP_SIZE = 7.0f;
        internal static float SEARCH_MIN_DISTANCE = 10.0f;
        internal static float SEARCH_MAX_DISTANCE = 45.0f;
        internal static float SEARCH_RADIUS = 15.0f;
        internal static float HEADING_THRESHOLD = 20.0f;
        internal static int TRAFFIC_LIGHT_POLL_FREQUENCY_MS = 750;
        internal static int TRAFFIC_LIGHT_GREEN_DURATION_MS = 5000;
        internal static Ped Player => Game.LocalPlayer.Character;
        internal static bool IsOpticomOn = false;
        internal static bool TrafficStopped = false;

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
        
        internal static bool IsDriverInPursuit(Ped p)
        {
            if(Functions.GetActivePursuit() == null) return false;
            return Functions.GetPursuitPeds(Functions.GetActivePursuit()).Contains(p);
        } 
        
        internal static void SetTrafficLightGreen(Object trafficLight)
        {
            GameFiber.StartNew(() =>
            {
                TrafficStopped = true;
                Vehicle[] nearbyVehs = Player.GetNearbyVehicles(16);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
                foreach (Vehicle v in nearbyVehs)
                {
                    if ((v && v.Driver) && (v == Player.CurrentVehicle || IsDriverInPursuit(v.Driver) || v.Model.IsEmergencyVehicle)) continue;
                    if(v && v.Driver) v.Driver.Tasks.PerformDrivingManeuver(v, VehicleManeuver.GoForwardStraightBraking, 2000);
                }
                GameFiber.Wait(TRAFFIC_LIGHT_GREEN_DURATION_MS);
                NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
                TrafficStopped = false;
            });
        }

        internal static Vector3 TranslateVector3(Vector3 pos, float angle, float distance)
        {
            var angleRad = angle * 2.0 * Math.PI / 360.0;
            return new Vector3((float)(pos.X - distance * Math.Sin(angleRad)), (float)(pos.Y + distance * Math.Cos(angleRad)), pos.Z);
        }

        internal static void CheckKeybind()
        {
            GameFiber.StartNew(() =>
            {
                while (true)
                {
                    GameFiber.Yield();
                    if (Game.IsKeyDown(Settings.ToggleKey))
                    {
                        IsOpticomOn = !IsOpticomOn;
                        Game.LogTrivial("Changing status");
                        Game.DisplaySubtitle("Opticom is " + (IsOpticomOn ? "~g~on~w~" : "~r~off~w~"));
                    }
                }
            });
        }

        internal static void Main()
        {
            CheckKeybind();
            GameFiber.StartNew(() =>
            {
                while (true)
                {
                    GameFiber.Yield();
                    if (!IsOpticomOn || TrafficStopped) continue;
                    GameFiber.Wait(TRAFFIC_LIGHT_POLL_FREQUENCY_MS);
                    if (Player.IsInAnyVehicle(false) && Player.CurrentVehicle)
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

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += LSPDFRFunctions_OnOnDutyStateChanged;
        }

        private void LSPDFRFunctions_OnOnDutyStateChanged(bool onduty)
        {
            if (onduty)
            {
                Settings.Initialize();
                Main();
            }
        }

        public override void Finally()
        {
        }
    }
}
