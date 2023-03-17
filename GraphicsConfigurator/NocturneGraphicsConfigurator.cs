
//c# stuff
using System.Reflection;
using System.IO;

using System.Collections.Generic;

//unity stuff
using MelonLoader;
using static MelonLoader.MelonLogger;
using UnityEngine;
using HarmonyLib;

//game stuff
using Il2Cppbasic_H;
using Il2CppXRD773Unity;
using Il2Cpp;
using Il2CppInterop.Runtime;
using System.Collections;
using System.Linq;

[assembly: MelonInfo(typeof(Nocturne_Graphics_Configurator.NocturneGraphicsConfigurator), "Nocturne Graphics Configurator", "1.0", "vv--")]
[assembly: MelonGame(null, "smt3hd")]


namespace Nocturne_Graphics_Configurator
{
    public class NocturneGraphicsConfigurator : MelonMod
    {

        //all of the config settings
        public static MelonPreferences_Category ModSettings;
        public static MelonPreferences_Entry<int> Framerate;
        public static MelonPreferences_Entry<int> VSyncMode;
        //public static MelonPreferences_Entry<Vector2Int> ResolutionOverride;
        public static MelonPreferences_Entry<string> FramerateToggleKey;
        public static MelonPreferences_Entry<bool> CustomFramerateOnLaunch;
        public static MelonPreferences_Entry<string> UIToggleKey;
        public static MelonPreferences_Entry<string> SpeedhackToggleKey;
        public static MelonPreferences_Entry<bool> BloomEnabled;

        //internal bools for those variables
        public static bool speedhack = false;
        public static bool Unlock = false;
        public static bool UIOn = true;
        public static int CurrentFramerate = 30;
        
        //internal ints to set/restore vsync non destructively
        public static int currentvsync;
        public static int originalvsync;

        //global objects
        public static GameObject ui;
        public static PostProcessStackV2Manager ppsv2;


        
        //whitelist of UI objects to hide/show
        public static List<string> uiremoves = new List<string>() { "talkUI", "fieldUI", "bparty(Clone)", "bmenuset(Clone)", "buttonguide01", "fldName", "bturnset(Clone)", "bannounce(Clone)" };

        //allows debug key to be pressed, used to be used much more extensively but 0.6+ hot reload removed a lot of the need for it
        public static bool MODDEBUGMODE = false;

        //debug value to show/hide interpolation target objects
        public static bool targsvis = false;


        #region MELONLOADER INIT + REGULAR FUNCTIONS

        //Config and initialization
        public override void OnInitializeMelon()
        {

            ModSettings = MelonPreferences.CreateCategory("ModSettings");
            BloomEnabled = ModSettings.CreateEntry("Bloom", true);
            Framerate = ModSettings.CreateEntry("Framerate", 60);
            CustomFramerateOnLaunch = ModSettings.CreateEntry("CustomFramerateOnLaunch", false);
            //ResolutionOverride = ModSettings.CreateEntry("ResolutionOverride", new Vector2Int(1920, 1080));
            VSyncMode = ModSettings.CreateEntry("VSyncModeWhenCustomFramerate", 1);
            FramerateToggleKey = ModSettings.CreateEntry("FramerateToggleKey", "f11");
            SpeedhackToggleKey = ModSettings.CreateEntry("SpeedhackToggleKey", "f10");
            UIToggleKey = ModSettings.CreateEntry("UIToggleKey", "f9");

            string path = "NocturneGraphicsConfiguratorConfig.cfg";

            ModSettings.SetFilePath(path);

            if (File.Exists(path))
            {
                ModSettings.LoadFromFile();
            }
            else
            {
                ModSettings.SaveToFile();
            }

            originalvsync = QualitySettings.vSyncCount;
            currentvsync = originalvsync;

            Unlock = CustomFramerateOnLaunch.Value;

            if (Unlock)
            {
                CurrentFramerate = Framerate.Value;
                currentvsync = VSyncMode.Value;
            }

            currentFrameTime = Time.realtimeSinceStartup;
            MelonCoroutines.Start(WaitForNextFrame());

        }



        //scan for canvas_ui, if found then scan for whitelisted objects and add canvasgroups to any that dont have them so they can be non-destructively hidden
        public static void updateui()
        {
            var b = 0;
            if (UIOn)
            {
                b = 1;
            }

            if (ui == null)
            {
                ui = GameObject.Find("Canvas_UI");
            }

            if (ui != null)
            {
                //constantly finds strings because objects get added/removed often
                foreach (string x in uiremoves)
                {
                    var find = ui.transform.Find(x);
                    if (find != null)
                    {
                        if (find.gameObject.GetComponent<CanvasGroup>() == null)
                        {
                            find.gameObject.AddComponent<CanvasGroup>();
                        }

                        var Component = find.gameObject.GetComponent<CanvasGroup>();

                        //this needs fixing. setting alpha on a canvasgroup that actually already existed before will cause some visibility bugs
                        //the intention of this logic was to only set alpha when Im sure I should be setting it.
                        if (Component.alpha == 0 || Component.alpha == 1)
                        {
                            Component.alpha = b;
                        }
                    }
                }
            }
        }


        //regular lateupdate function, used for constant mod input and configuration
        public override void OnLateUpdate()
        {

            if (Application.targetFrameRate != CurrentFramerate)
            {
                Application.targetFrameRate = CurrentFramerate;
            }

            if (QualitySettings.vSyncCount != currentvsync)
            {
                QualitySettings.vSyncCount = currentvsync;
            }

            if (Input.GetKeyDown(FramerateToggleKey.Value))
            {
                Unlock = !Unlock;

                LoggerInstance.Msg("Framerate mod set to " + Unlock.ToString());
                
                currentFrameTime = Time.realtimeSinceStartup;

                if (Unlock)
                {
                    CurrentFramerate = Framerate.Value;
                    currentvsync = VSyncMode.Value;

                }
                else
                {
                    CurrentFramerate = 9999;
                    currentvsync = originalvsync;
                }

            }


            if (Input.GetKeyDown(UIToggleKey.Value))
            {

                UIOn = !UIOn;

                LoggerInstance.Msg("UI set to " + UIOn.ToString());

            }

            if (Input.GetKeyDown(SpeedhackToggleKey.Value))
            {

                speedhack = !speedhack;

                LoggerInstance.Msg("Speedhack set to " + speedhack.ToString());


            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (MODDEBUGMODE)
                {
                    MelonLogger.Msg("debug key pressed");

                    MelonLogger.Msg("interpolation target visualizers toggled");
                    targsvis = !targsvis;

                    unitobjectdetails();

                }

            }


            if (speedhack)
            {
                Time.fixedDeltaTime = 1f / CurrentFramerate;
            }
            else
            {
                Time.fixedDeltaTime = 1f / 30f;
            }


            //debug visualise interpolation objects
            foreach (KeyValuePair<dds3Basic_t, GameObject> x in interpolationobjlist)
            {
                if (x.Value != null && x.Value.GetComponent<MeshRenderer>() != null)
                {
                    x.Value.GetComponent<MeshRenderer>().enabled = targsvis;
                }
            }


            //done this way so game can still disable bloom if it wants
            if (ppsv2 != null && !BloomEnabled.Value)
            {
                ppsv2.bloom.active = false;
            }

            updateui();
        }


        //debug function to print information about tracked characters
        public static void unitobjectdetails()
        {

            MelonLogger.Msg("these are the current unitobject setups");
            foreach (KeyValuePair<dds3Basic_t, GameObject> epic in interpolationobjlist)
            {

                MelonLogger.Msg($"{epic.Key.name} (basic serial id: {epic.Key.serialID.ToString()}, unity object id: {epic.Key.gameObject.GetInstanceID().ToString()}): {epic.Key.gameObject.transform.position.ToString()},  {epic.Value.name} :  {epic.Value.transform.position.ToString()}");
            }

        }


        #endregion


        /// <summary>
        /// PATCHES BEGIN HERE
        /// </summary>

        #region CORE PATCHES



        //kills the default renderforcer responsible for enforcing 30fps

        [HarmonyPatch(typeof(ForceRenderRate), "Start")]
        public static class RenderForcerPatch
        {
            public static void Prefix(ref ForceRenderRate __instance, ref bool __runOriginal)
            {
                __runOriginal = false;
                GameObject.Destroy(__instance.gameObject);
            }
        }

        //the renderforcer uses this exact code literally ripped from a unity blog lmao https://blog.unity.com/technology/precise-framerates-in-unity
        //though its been modified to set itself to 30 constantly, or some other object does that, Im not sure. Im not dealing with it, instead Im replacing it.

        //ive modified that coroutine a little and added it here, started up in the init function

        float currentFrameTime;
        IEnumerator WaitForNextFrame()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                currentFrameTime += 1.0f / 30f;
                var t = Time.realtimeSinceStartup;
                var sleepTime = currentFrameTime - t - 0.01f;


                //should fix stalls?
                if (!Application.isFocused)
                {
                    currentFrameTime = Time.realtimeSinceStartup;
                }


                if (!Unlock)
                {
                    if (sleepTime > 0)
                        System.Threading.Thread.Sleep((int)(sleepTime * 1000));
                    while (t < currentFrameTime)
                        t = Time.realtimeSinceStartup;
                }

            }
        }






        public static dds3DefaultMain kernel;


        //these are various bools to control the interpolation and other visual patches being active or not

        public static bool catchup = false;
        public static bool gamelogicrun = false;
        public static bool boolforafterdoneloop = false;
        public static bool boolforlateframe = false;


        //makes the game loop method get called from this script instead of from the actual game
        //that way I can place it into fixedupdate and separate it from the actual target framerate
        public override void OnFixedUpdate()
        {
            //allow methods to run as originally programmed if not unlocked
            if (!Unlock || !Application.isFocused)
            {
                return;
            }

            gamelogicrun = true;
            boolforafterdoneloop = true;
            boolforlateframe = true;
            if (kernel != null && kernel.isActiveAndEnabled && kernel.initflag)
            {
                catchup = false;
                dds3KernelMain.m_dds3KernelMainLoop();

                //this is the only way I can make this work for some reason, call the main game loop to process 1 frame ahead
                if (catchup)
                {
                    dds3KernelMain.m_dds3KernelMainLoop();
                }

                //Catchup();
            }
            gamelogicrun = false;
        }

        //makes sure the game loop is only ran on fixedupdate (if unlocked)
        [HarmonyPatch(typeof(dds3KernelMain), "m_dds3KernelMainLoop")]
        public static class mainloop
        {
            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = gamelogicrun || !Unlock;
                
            }
        }


        //Returns true at a rate that should be almost exactly 30fps
        public static bool Determine30fps()
        {
            return (boolforafterdoneloop || speedhack);
        }

        //Returns true at a rate that should be almost exactly 30fps but set forward one frame
        //mainly use this for lateupdate patches and/or patches that affect other objects
        public static bool DetermineLate30fps()
        {
            return (boolforlateframe || speedhack);
        }


        //call this when you want to warp objects to real positions immediately to prevent interpolation jitter, eg when teleporting
        public static void Catchup()
        {
            //Msg("catchup");
            catchup = true;

        }



        //Patch to track crucial/character objects
        public static Dictionary<dds3Basic_t, GameObject> interpolationobjlist = new Dictionary<dds3Basic_t, GameObject>();

        //trackers for camera, characters are in interpolationobjlist
        public static GameObject cam;
        public static GameObject camposlast;

        //debug timer to measure realframe time
        public static System.Diagnostics.Stopwatch realframetime = new System.Diagnostics.Stopwatch();


        //this exists so I can set the control bool during the start of the next frame, to allow lateupdate patches to work
        public static bool lateupdatetag = false;


        public static float RealDeltaTime = 0.0f;


        //Patches the main loop of the game - the logic here interpolates objects on non-real frames and clears crucial lists
        //THIS SHOULD RUN AT THE ACTUAL FRAMERATE, NOT 30. 

        //the original intention of this logic was to kind of sandwich the normal logic, for example (prefix -> game loop -> postfix), (prefix -> no game loop but interpolation -> postfix), and so on..
        //but now that the logic is being called from fixedupdate instead of nestled in here in a determined way, this kind of just acts in parallel in any execution order it feels like.
        //Im honestly surprised it even works, it seems like it shouldnt. the whole reason I didnt initially use fixedupdate initially was I thought it wouldnt work (at least not smoothly).


        [HarmonyPatch(typeof(dds3DefaultMain), "Update")]
        public static class MainLoopPatch
        {
            public static void Prefix(ref dds3DefaultMain __instance)
            {

                if (!Application.isFocused)
                {
                    return;
                }

                if (!boolforafterdoneloop && boolforlateframe)
                {
                    boolforlateframe = false;
                }


                //this still somehow allows some transition errors not present in OG and I dont know why, may be out of my control?
                if (!Unlock)
                {
                    gamelogicrun = true;
                    boolforafterdoneloop = true;
                    boolforlateframe = true;

                }


                //fixes the entire keyboard missed input issue, I kid you not
                //it seems like this works by tricking the game into looking for keyboard inputs constantly instead of only on real frames
                SteamInputUtil.Instance.bAnyKeyDown = true;


                kernel = __instance;


                //did this because reading deltatime from a fixedupdate call very unhelpfully just gives you fixeddeltatime
                RealDeltaTime = Time.deltaTime;


                //if a new frame
                if (!Determine30fps())
                {
                    float delcomp = 30f / (1f / RealDeltaTime);

                    //interpolate unit objects
                    if (interpolationobjlist != null)
                    {
                        List<dds3Basic_t> args = new List<dds3Basic_t>(interpolationobjlist.Keys);

                        foreach (dds3Basic_t x in args)
                        {
                            if (x != null && x.gameObject != null && x.gameObject.transform.childCount != 0 && interpolationobjlist[x] != null)
                            {
                                Transform dest = interpolationobjlist[x].transform;

                                //get child if its a battle demon

                                GameObject ch = x.gameObject.transform.GetChild(0).gameObject;

                                if (ch.GetComponent(Il2CppType.Of<AnimCheckerBattle>()) == null)
                                {
                                    ch = x.gameObject;
                                }

                                if (Vector3.Distance(ch.transform.position, dest.position) < 2.5f && !catchup && Unlock)
                                {
                                    ch.transform.position = Vector3.Lerp(ch.transform.position, dest.position, delcomp);
                                    ch.transform.rotation = Quaternion.Slerp(ch.transform.rotation, dest.rotation, delcomp);
                                }
                                else
                                {
                                    ch.transform.position = dest.position;
                                    ch.transform.rotation = dest.rotation;
                                }

                            }

                        }
                    }

                    //interpolate camera
                    if (cam != null && camposlast != null)
                    {
                        //hopefully a fair distance to stop interpolating at

                        if (Vector3.Distance(cam.transform.position, camposlast.transform.position) < 2.5f && !catchup && Unlock)
                        {
                            cam.transform.position = Vector3.Lerp(cam.transform.position, camposlast.transform.position, delcomp);
                            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, camposlast.transform.rotation, delcomp);

                        }
                        else
                        {
                            cam.transform.position = camposlast.transform.position;
                            cam.transform.rotation = camposlast.transform.rotation;
                        }

                    }

                }

            }

            public static void Postfix()
            {
                if (!Application.isFocused)
                {
                    return;
                }

                ///if original frame, set everything back to last original frame and store intended current frame

                if (Determine30fps())
                {
                    if (interpolationobjlist != null && interpolationobjlist.Count != 0)
                    {
                        List<dds3Basic_t> args = new List<dds3Basic_t>(interpolationobjlist.Keys);

                        foreach (dds3Basic_t x in args)
                        {
                            if (x != null && x.gameObject != null && x.gameObject.transform.childCount != 0 && interpolationobjlist[x] != null)
                            {

                                //interpolate first child if its a battle demon
                                //might need to add more cases to this

                                GameObject ch = x.gameObject.transform.GetChild(0).gameObject;

                                if (ch.GetComponent(Il2CppType.Of<AnimCheckerBattle>()) == null)
                                {
                                    ch = x.gameObject;
                                }

                                Vector3 np = ch.transform.position;
                                Quaternion nr = ch.transform.rotation;


                                if (!speedhack && Unlock && !catchup)
                                {
                                    ch.transform.position = interpolationobjlist[x].transform.position;
                                    ch.transform.rotation = interpolationobjlist[x].transform.rotation;
                                }


                                interpolationobjlist[x].transform.position = np;
                                interpolationobjlist[x].transform.rotation = nr;

                            }

                        }

                    }

                    if (kernel != null)
                    {
                        cam = kernel.GetMainCamera();

                        if (cam != null && cam.transform != null)
                        {
                            Vector3 np = cam.transform.position;
                            Quaternion nr = cam.transform.rotation;

                            if (camposlast != null)
                            {
                                if (!speedhack && Unlock && !catchup)
                                {
                                    cam.transform.position = camposlast.transform.position;
                                    cam.transform.rotation = camposlast.transform.rotation;
                                }

                            }
                            else
                            {
                                camposlast = new GameObject();
                            }

                            camposlast.transform.position = np;
                            camposlast.transform.rotation = nr;

                        }
                    }

                }

                ///IF NOT AN ORIGINAL FRAME
                else
                {

                    //clear any outdated interpolation trackers because the delete/destroy calls are either unreliable or not used

                    Dictionary<dds3Basic_t, GameObject> newl = new Dictionary<dds3Basic_t, GameObject>();

                    Dictionary<dds3Basic_t, int> checker = new Dictionary<dds3Basic_t, int>();

                    foreach (KeyValuePair<dds3Basic_t, GameObject> x in interpolationobjlist)
                    {

                        //dont keep duplicate empties
                        if (x.Key != null && x.Key.gameObject != null && checker.ContainsValue(x.Key.gameObject.GetInstanceID()))
                        {
                            GameObject.Destroy(interpolationobjlist[x.Key]);
                            if (MODDEBUGMODE)
                            {
                                MelonLogger.Msg("duplicate found and destroyed");
                            }
                            continue;
                        }

                        //is basictype is valid then keep it
                        if (x.Key != null && x.Key.gameObject != null && x.Value != null)
                        {
                            newl.Add(x.Key, x.Value);
                            checker.Add(x.Key, x.Key.gameObject.GetInstanceID());
                            x.Value.name = x.Key.serialID + "_frameVVmod";
                        }

                        //if basictype isnt valid then dont keep it, and if the tracker object exists still then delete it
                        else if (x.Value != null)
                        {
                            GameObject.Destroy(x.Value);
                        }

                    }

                    if (MODDEBUGMODE && interpolationobjlist.Count != newl.Count)
                    {
                        MelonLogger.Msg("removed " + (interpolationobjlist.Count - newl.Count).ToString() + " unitobject interpolator entries");
                    }

                    interpolationobjlist = newl;

                }

                boolforafterdoneloop = false;
            }

        }



        //Patch to list crucial/character objects for above interpolation

        [HarmonyPatch(typeof(dds3UnitObjectBasic), "dds3AddUnitObjectBasic")]
        public static class InstanceStorage
        {
            public static void Postfix(ref dds3Basic_t __result)
            {

                //creates a debug sphere 
                GameObject empty = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                empty.transform.localScale = Vector3.one * 0.2f;
                empty.GetComponent<MeshRenderer>().enabled = false;

                if (MODDEBUGMODE)
                {
                    MelonLogger.Msg("added a basictype - ", interpolationobjlist.Count);
                }

                empty.transform.position = new Vector3(0, 9999, 0);

                interpolationobjlist.Add(__result, empty);

            }
        }



        //place methods here that teleport objects so they can use Catchup() to prevent incorrect interpolation transition

        [HarmonyPatch]
        public static class TeleportationPatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                //prevent opening/closing map jitter
                yield return AccessTools.Method(typeof(fldAutoMap), "fldAutoMapSeqEnd");
                yield return AccessTools.Method(typeof(fldAutoMap), "fldAutoMapSeqStart");

                //this one I'm using as a target for area/subarea loading just like minimap mod
                yield return AccessTools.Method(typeof(fldTitle), "fldTitleMiniStart2");


                //these are for object teleportation moments in battle
                //not sure if this is thorough or not thorough enough, but it should cover almost all camera situations

                foreach (MethodBase m in typeof(nbCameraSkill).GetMethods().Union(typeof(nbCameraBoss).GetMethods()))
                {
                    if (((m.Name.Contains("Cut") && m.Name.Contains("Init")) || (m.Name.Contains("Init") && !m.Name.Contains("End"))) && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }

                //yield return AccessTools.Method(typeof(nbCameraSkill), "nbCameraState_SkillAttack_Init");

                foreach (MethodBase m in typeof(nbCameraCommand).GetMethods())
                {
                    if (m.Name.Contains("_Set") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }


                
            }

            public static void Postfix()
            {
                Catchup();
            }

        }




        //this patches a variety of methods that need to be locked to 30fps
        [HarmonyPatch]
        public static class LockToLate30FPS
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {

                //philosophy is to patch as little as possible here, patching multiple things or trying to create new calls often goes wrong


                //fixes particles flickering
                yield return typeof(GraphicManager.CommonObject).GetMethod("LateUpdate");


                //fixes demon model flickering
                yield return AccessTools.Method(typeof(ModelHD), "CallLateUpdate");


                //fix flickering bloom
                yield return AccessTools.Method(typeof(PostProcessStackV2Manager), "Update");
                   

                //part of a fix for flickering accum blur processing, rest is at the bottom
                yield return AccessTools.Method(typeof(PostBlur), "Update");
                

                //not sure if this is needed or not, just added it anyway
                yield return AccessTools.Method(typeof(RippleEffect), "Update");


                //fix scrolling text speed
                yield return AccessTools.Method(typeof(ScrollText), "Update");


            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = DetermineLate30fps();

            }

        }




        #endregion



        #region INPUT PATCHES

        //mouse speed patch to counteract original deltatime calculation
        [HarmonyPatch(typeof(SteamMouse), "Update")]
        public static class MousePatch
        {
            public static void Postfix(ref SteamMouse __instance)
            {
                //MelonLogger.Msg(1f / Time.deltaTime);
                __instance.Axis = ((__instance.Axis / RealDeltaTime) / 30f);
            }
        }

        //fix for keyboard input dropping is in the big update patch because its comically simple and small


        #endregion


        #region MISCELLANEOUS PATCHES



        //patch to ensure battle demons and other animations that rely on this have unlocked animation framerates
        [HarmonyPatch(typeof(FrameAnime), "LateUpdate")]
        public static class FrameAnimePatch
        {
            public static void Postfix(ref FrameAnime __instance, ref bool __runOriginal)
            {
                __instance.time = RealDeltaTime;

                //there might be a better way to do this but this works without issue I think
                __instance.StepFrame();

            }
        }



        //keeps track of post processing object for easy bloom enable/disable
        [HarmonyPatch(typeof(PostProcessStackV2Manager), "Update")]
        public static class postprocesstrack
        {
            public static void Postfix(ref PostProcessStackV2Manager __instance)
            {
                ppsv2 = __instance;
            }
        }



        //patches flickering accumulation motion blur processing aka "flickering lighting"
        //not sure if this needs to be exactly this complicated but this works

        public static bool refreshblit = true;
        public static bool nowmodetemp = false;

        [HarmonyPatch(typeof(PostBlur), "OnRenderImage")]
        public static class renderfix1
        {

            public static void Prefix(ref PostBlur __instance)
            {
                if (DetermineLate30fps())
                {
                    nowmodetemp = dds3KernelDraw.Postblur_AccumTex_NowMode;
                }

                if (Unlock && !speedhack && nowmodetemp)
                {
                    __instance.refresh_Blit = refreshblit;
                }
            }
        }


        [HarmonyPatch(typeof(dds3DefaultMain), "FieldFilterOn")]
        public static class FieldFilter1
        {
            public static void Prefix()
            {
                refreshblit = true;
            }
        }

        [HarmonyPatch(typeof(dds3DefaultMain), "FieldFilterOff")]
        public static class FieldFilter2
        {
            public static void Prefix()
            {
                refreshblit = false;
            }
        }



        #endregion


        /// <summary>
        /// PATCHES END HERE
        /// </summary>


    }
}
