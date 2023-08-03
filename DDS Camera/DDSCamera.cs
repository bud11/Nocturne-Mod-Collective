using System;

using HarmonyLib;
using MelonLoader;
using static MelonLoader.MelonLogger;
using Il2Cpp;
using DDSFixedCamera;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpplibsdf_H;


[assembly: MelonInfo(typeof(dds), "DDS-style fixed camera", "1.31", "vvdashdash")]
[assembly: MelonGame(null, "smt3hd")]

namespace DDSFixedCamera
{
    public class dds : MelonMod
    {

        //this mod is the most hacky piece of shit ever, do NOT learn from this...

        //this mod allows you to move in 360 degrees by tricking the game into believing you're holding forwards no matter what direction you're actually holding
        //then it takes advantage of the fact that player movement is based around the camera viewing angle to tell the game that the camera viewing angle is the actual direction you're holding
        //then it runs the entire playerCalc_nml AGAIN because that process is horrendous and breaks regular camera functions, but also disables player transform modifications to prevent double speed
        //luckily playerCalc_nml seems to contain just enough to make this work but not so much that it breaks

        //and all of that bullshit is because the move direction is natively hardcoded to 8 directions for some reason... so I just use one

        //well.. I say this works, but it doesnt. it breaks event triggers. Probably something to do with collisions
        
        //main camera patch
        [HarmonyPatch(typeof(fldCamera), "fldCamMain")]
        public static class camerapatch
        {
            public static void Prefix()
            {
                //responsible for the rotation you get when you turn
                fldPlayer.gJidouRotSpeed = 0;
            }
        }


        //prevents the auto 360/8 increment rotation automation when your camera isnt already at an exact 360/8 angle
        [HarmonyPatch(typeof(fldPlayer), "fldPlayerCalc360_8")]
        public static class camerapatch2
        {
            public static void Prefix(ref bool __runOriginal, ref float __result, ref float aKakudo)
            {
                
                //fldGlobal.fldGb.PlayerKakudo = lockgoal;

                if (disablebehavior)
                {
                    __result = fldPlayer.fldPlayerCalc360_4(aKakudo);
                    __runOriginal = false;
                }
                else
                {
                    __result = aKakudo;
                    __runOriginal = false;
                }
            }
        }


        public static bool Forcing = false;

        public static float tempy = 0f;

        public static float inpstr = 0f;

        public static bool secondrun = false;

        public static bool disablebehavior = false;




        //basically the same thing but for the player input relative to camera
        [HarmonyPatch(typeof(fldPlayer), "fldPlayerCalc_Nml")]
        public static class movepatch
        {
            public static void Prefix()
            {
                if (!disablebehavior && !secondrun)
                {
                    //responsible for the 8-dir snapping you get rotated by camera
                    float lr = (dds3PadManager.GetPadAnalog(0, 0, 0) / 128f) - 1f;
                    float ud = (dds3PadManager.GetPadAnalog(0, 0, 1) / 128f) - 1f;

                    if (Mathf.Abs(lr) <= 0.5)
                    {
                        lr = 0;
                    }
                    if (Mathf.Abs(ud) <= 0.5)
                    {
                        ud = 0;
                    }

                    inpstr = new Vector2(lr, ud).magnitude;

                    float newR = (Mathf.Rad2Deg * (float)Math.Atan2(lr, ud) * -1f);

                    tempy = fldTest.ooyCamKakudo;

                    fldTest.ooyCamKakudo = Mathf.Repeat(-newR + fldTest.ooyCamKakudo, 360f);

                    Forcing = true;
                }

            }
            public static void Postfix()
            {
                if (!disablebehavior && !secondrun)
                {
                    Forcing = false;

                    fldTest.ooyCamKakudo = tempy;

                    secondrun = true;
                    fldPlayer.fldPlayerCalc_Nml();
                }
                secondrun = false;
            }
        }


        //pad analog patch to force forward direction regardless of actual axis during my transformations
        [HarmonyPatch(typeof(dds3PadManager), "GetPadAnalog")]
        public static class analogpatch
        {
            public static void Prefix(ref int stick_lr, ref int xy, ref byte __result, ref bool __runOriginal)
            {
                if (secondrun)
                {
                    __result = 128;
                    __runOriginal = false;
                    return;
                }

                if (Forcing && stick_lr == 0)
                {
                    //if trying to get forward move stick, tell it its holding forwards as much as the user is actually inputting in any direction
                    if (xy == 1)
                    {
                        __result = (byte)(128 + (127*Mathf.Clamp(inpstr,0,1)));
                    }

                    //otherwise tell it nothing
                    else
                    {
                        __result = 128;
                    }
                    __runOriginal = false;
                }
            }
        }


        //uhh yeah
        //this is designed to stop logic in the second iteration from actually doing anything to the player, the second iteration only exists to make the camera buttons work properly

        [HarmonyPriority(1000)]
        [HarmonyPatch]
        public static class objectlock
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(dds3Basic), "dds3SetRotPoseSet");
                yield return AccessTools.Method(typeof(dds3Basic), "dds3SetPosPoseSet");
                yield return AccessTools.Method(typeof(dds3Basic), "dds3SetSclPoseSet");
            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = !secondrun;
            }
        }


        //check for input
        [HarmonyPatch(typeof(dds3KernelMain), "m_dds3KernelMainLoop")]
        public static class inpchk
        {
            public static void Prefix()
            {
                if (!Application.isFocused)
                {
                    return;
                }
                secondrun = false;
                Forcing = false;
                disablebehavior = fldGlobal.fldGb.NoInpPlCnt <= 0 && fldGlobal.fldGb.cammode == 0 && (dds3PadManager.DDS3_PADCHECK_PRESS(SDF_PADMAP.RD) || (dds3PadManager.DDS3_PADCHECK_PRESS(SDF_PADMAP.L1) && dds3PadManager.DDS3_PADCHECK_PRESS(SDF_PADMAP.R1)));

            }
        }

    }
}
