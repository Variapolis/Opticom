using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using Object = Rage.Object;

namespace Opticom
{
    
    internal class EntryPoint : Plugin
    {
        internal static float HeadingThreshold = 20.0f;
        private static int OverrideDuration = 5000;
        private static Ped Player => Game.LocalPlayer.Character;
        private static bool IsOpticomOn = false;
        internal static bool TrafficStopped = false;

        private static readonly HashSet<uint> TrafficLightModels = new()
        {
            0x3e2b73a4,
            0x336e5e2a,
            0xd8eba922,
            0xd4729f50,
            0x272244b2,
            0x33986eae,
            0x2323cdc5
        };

        private const float sqrRootOfTwo = 1.41421356237f;
        private const float intersectionScale = 1.5f;

        private static readonly Dictionary<Object, uint> DebugDict = new();

        private static float _maxDistSqr;

        private static readonly Vector3 VectorOne = new(1, 1, 1);

        private static List<Vector3> _checkedNodes = new();
        private static float zThreshold = 5f;
        
        internal static bool IsDriverInPursuit(Ped p)
        {
            if(Functions.GetActivePursuit() == null) return false;
            return Functions.GetPursuitPeds(Functions.GetActivePursuit()).Contains(p);
        } 
        
        // internal static void SetTrafficLightGreen(Object trafficLight)
        // {
        //     GameFiber.StartNew(() =>
        //     {
        //         TrafficStopped = true;
        //         Vehicle[] nearbyVehs = Player.GetNearbyVehicles(16);
        //         NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
        //         foreach (Vehicle v in nearbyVehs)
        //         {
        //             if ((v && v.Driver) && (v == Player.CurrentVehicle || IsDriverInPursuit(v.Driver) || v.Model.IsEmergencyVehicle)) continue;
        //             if(v && v.Driver) v.Driver.Tasks.PerformDrivingManeuver(v, VehicleManeuver.GoForwardStraightBraking, 2000);
        //         }
        //         GameFiber.Wait(TRAFFIC_LIGHT_GREEN_DURATION_MS);
        //         NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 3);
        //         TrafficStopped = false;
        //     });
        // }

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

        // internal static void Main()
        // {
        //     CheckKeybind();
        //     GameFiber.StartNew(() =>
        //     {
        //         while (true)
        //         {
        //             GameFiber.Yield();
        //             if (!IsOpticomOn || TrafficStopped) continue;
        //             GameFiber.Wait(TRAFFIC_LIGHT_POLL_FREQUENCY_MS);
        //             if (Player.IsInAnyVehicle(false) && Player.CurrentVehicle)
        //             {
        //                 Vector3 position = Player.Position;
        //                 float heading = Player.Heading;
        //                 Object trafficLight = null;
        //                 for (float searchDistance = SEARCH_MAX_DISTANCE;
        //                      searchDistance > SEARCH_MIN_DISTANCE;
        //                      searchDistance -= SEARCH_STEP_SIZE)
        //                 {
        //                     Vector3 SearchPosition = TranslateVector3(position, heading, searchDistance);
        //                     foreach (uint tro in trafficLightObjects)
        //                     {
        //                         trafficLight = NativeFunction.Natives.GET_CLOSEST_OBJECT_OF_TYPE<Object>(SearchPosition,
        //                             SEARCH_RADIUS, tro, false, false, false);
        //
        //                         if (trafficLight != null)
        //                         {
        //                             var lightHeading = trafficLight.Heading;
        //                             var headingDiff = Math.Abs(heading - lightHeading);
        //
        //                             if (headingDiff < HEADING_THRESHOLD || headingDiff > (360 - HEADING_THRESHOLD))
        //                             {
        //                                 SetTrafficLightGreen(trafficLight);
        //                                 break;
        //                             }
        //                             else
        //                             {
        //                                 trafficLight = null;
        //                             }
        //                         }
        //                     }
        //                 }
        //
        //             }
        //         }
        //     });
        // }

        internal static void OverrideTrafficLight(Object trafficLight)
        {
            // TODO: Add the lights to the timeout dictionary.
            NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(trafficLight, 0);
        }

        public static void Main()
        {
            GameFiber.StartNew(() =>
            {
                if(!IsOpticomOn || TrafficStopped) { GameFiber.SleepWhile(() => !IsOpticomOn || TrafficStopped, 0); }
                var maxDist = 35;
                _maxDistSqr = maxDist * maxDist;
                var timer = new Stopwatch();
                while (true)
                {
                    _checkedNodes.Clear();
                    timer.Start();
                    FindIntersectionAheadOfPlayer();
                    CheckTrafficLightTimeout();
                    timer.Stop();
                    Game.DisplaySubtitle(timer.Elapsed.Milliseconds.ToString());
                    timer.Reset();
                    GameFiber.Yield();
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        private static void CheckTrafficLightTimeout()
        {
            foreach (var pair in DebugDict)
            {
                // TODO: Add traffic control
                if (pair.Value > Game.GameTime + OverrideDuration)
                {
                    NativeFunction.Natives.SET_ENTITY_TRAFFICLIGHT_OVERRIDE(pair.Key, 3);
                }
            }
        }


        private static void FindIntersectionAheadOfPlayer()
        {
            // Estimates the direction of travel of the player by lerping between velocity and direction based on speed.
            var directionOfTravel =
                Vector3.Lerp(
                    Game.LocalPlayer.Character.Position + Game.LocalPlayer.Character.ForwardVector *
                    MathHelper.Clamp(Game.LocalPlayer.Character.Speed, 5f, 50f),
                    Game.LocalPlayer.Character.Position + Game.LocalPlayer.Character.Velocity,
                    Game.LocalPlayer.Character.Speed / 30);
            
            for (int i = 0; i < 5; i++)
            {
                if (NativeFunction.Natives.xE50E52416CCF948B<bool>(directionOfTravel.X,
                        directionOfTravel.Y, directionOfTravel.Z, i, out Vector3 traffLightNodePos))
                {
                    // Get Node Properties
                    NativeFunction.Natives.x0568566ACBB5DEDC<bool>(traffLightNodePos.X, traffLightNodePos.Y, traffLightNodePos.Z,
                        out int _, out int traffLightNodeFlags); 
                    
                    // If Node is Traffic Light Node and within the height threshold
                    if ((traffLightNodeFlags & 256) != 256 || 
                        !(Math.Abs(traffLightNodePos.Z - directionOfTravel.Z) < zThreshold)) continue; 
                    
                    for (int j = 0; j < 5; j++)
                    {
                        // Get Node Properties
                        if (!NativeFunction.Natives.xE50E52416CCF948B<bool>(directionOfTravel.X,
                                directionOfTravel.Y, directionOfTravel.Z, j,
                                out Vector3 intersectionNodePos)) break;
                        
                        // If Node is Intersection Node
                        if (!NativeFunction.Natives.x0568566ACBB5DEDC<bool>(intersectionNodePos.X,
                                intersectionNodePos.Y, intersectionNodePos.Z,
                                out int _, out int nodeFlags) || (nodeFlags & 128) != 128) continue;
                        
                        // TODO: This may need a zThreshold to prevent intersections underneath or above the player from activating.
                        
                        // Get the size of the intersection square diagonally
                        float intersectionSize = Vector3.Distance(traffLightNodePos, intersectionNodePos) * sqrRootOfTwo * intersectionScale;
                        OverrideTrafficLightsInRadius(intersectionNodePos, intersectionSize);
                    }
                }
            }
        }

        private static void OverrideTrafficLightsInRadius(Vector3 intersectionNodePos, float intersectionSize)
        {
            foreach (var ent in World.GetEntities(intersectionNodePos, intersectionSize, GetEntitiesFlags.ConsiderAllObjects))
            {
                if (TrafficLightModels.Contains(ent.Model.Hash) && ent is Object trafficLightObj)
                {
                    // TODO: Add red/green options and heading overrides.
                    // Debug.DrawWireBox(ent.Position, ent.Orientation, VectorOne, Color.Red);
                    OverrideTrafficLight(trafficLightObj);
                }
            }
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
