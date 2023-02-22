
//c# stuff
using System.Reflection;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

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

        }


        //Destroy render forcer object responsible for enforcing 30fps
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            var renderforcer = GameObject.Find("ForceRenderRate");

            if (renderforcer != null)
            {
                UnityEngine.Object.Destroy(renderforcer);
            }

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


        //Returns true at a rate that should be 30fps or at least very close regardless of the framerate
        public static bool Determine30fps()
        {
            //im an idiot and Im not sure what the correct math should be here.

            return (Math.Abs(FrameReal) < 0.99999999999999f || speedhack);
            //return (Math.Abs(FrameReal) < 0.01f || speedhack);
        }


        //regular lateupdate function, used for constant mod input and configuration
        public override void OnLateUpdate()
        {

            if (Application.targetFrameRate != CurrentFramerate)
            {
                Application.targetFrameRate = CurrentFramerate;
                Time.fixedDeltaTime = 1f / CurrentFramerate;
            }

            if (QualitySettings.vSyncCount != currentvsync)
            {
                QualitySettings.vSyncCount = currentvsync;
            }

            if (Input.GetKeyDown(FramerateToggleKey.Value))
            {
                Unlock = !Unlock;

                LoggerInstance.Msg("Framerate mod set to " + Unlock.ToString());

                if (Unlock)
                {
                    CurrentFramerate = Framerate.Value;
                    currentvsync = VSyncMode.Value;
                }
                else
                {
                    CurrentFramerate = 30;
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

        //Patch to track crucial/character objects
        public static Dictionary<dds3Basic_t, GameObject> interpolationobjlist = new Dictionary<dds3Basic_t, GameObject>();

        //trackers for camera, characters are in interpolationobjlist
        public static GameObject cam;
        public static GameObject camposlast;

        //modified in main update loop, used for nth frame 30fps calcuation
        public static float FrameReal = 0;

        //debug timer to measure realframe time
        public static System.Diagnostics.Stopwatch realframetime = new System.Diagnostics.Stopwatch();


        //Patches the main loop of the game - the logic here interpolates objects on non-real frames and clears crucial lists
        [HarmonyPatch(typeof(dds3DefaultMain), "Update")]
        public static class MainLoopPatch
        {
            public static void Prefix(ref dds3DefaultMain __instance)
            {

                FrameReal = (FrameReal + 1) % (CurrentFramerate / 30f);

                //if a new frame
                if (!Determine30fps())
                {
                    //call any calls from last frame to lift them up to 60
                    foreach(Tuple<dynamic, MethodBase, object[]> call in calls)
                    {
                        call.Item2.Invoke(call.Item1, call.Item3);
                    }

                    if (MODDEBUGMODE)
                    {
                        if (realframetime.IsRunning)
                        {
                            realframetime.Stop();
                            if (targsvis)
                            {
                                MelonLogger.Msg($"Since last real frame: {realframetime.ElapsedMilliseconds}ms");
                                MelonLogger.Msg(System.Drawing.Color.DarkBlue, $"game running at approximately {((1000 / 30f) / realframetime.ElapsedMilliseconds) * 100f}% speed");
                            }
                        }
                        realframetime.Restart();
                    }

                    float delcomp = 30f / CurrentFramerate;

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

                                if (Vector3.Distance(ch.transform.position, dest.position) < 2.5f)
                                {
                                    ch.transform.position = Vector3.Lerp(ch.transform.position, dest.position, delcomp);
                                    ch.transform.rotation = Quaternion.Slerp(ch.transform.rotation, dest.rotation, delcomp);
                                }
                                else
                                {
                                    ch.transform.position = dest.position;
                                    ch.transform.rotation = dest.rotation;
                                }

                                //ch.transform.position = Vector3.zero;

                            }

                        }
                    }

                    //interpolate camera
                    if (cam != null && camposlast != null)
                    {
                        //hopefully a fair distance to stop interpolating at

                        if (Vector3.Distance(cam.transform.position, camposlast.transform.position) < 2.5f)
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
                else
                {
                    lastframepresschecks.Clear();
                    calls.Clear();
                }

            }



            public static void Postfix()
            {
                ///if original frame, set everything back to last original frame and store intended current frame

                if (Determine30fps())
                {
                    inputretainpress.Clear();

                    /*keyspressed.Clear();*/

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

                                if (!speedhack && CurrentFramerate != 30)
                                {
                                    ch.transform.position = interpolationobjlist[x].transform.position;
                                    ch.transform.rotation = interpolationobjlist[x].transform.rotation;
                                }

                                interpolationobjlist[x].transform.position = np;
                                interpolationobjlist[x].transform.rotation = nr;

                            }

                        }

                    }

                    if (GlobalData.kernelObject != null)
                    {
                        cam = GlobalData.kernelObject.GetMainCamera();

                        if (cam != null && cam.transform != null)
                        {
                            Vector3 np = cam.transform.position;
                            Quaternion nr = cam.transform.rotation;

                            if (camposlast != null)
                            {
                                if (!speedhack && CurrentFramerate != 30)
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
                    //force certain calls to be one frame ahead to avoid interpolation lag
                    //fldPlayer.fldPlayerPosRot();
                    //fldPlayer.fldPlayerCalc();


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



            }

        }


        //Patch to list crucial/character objects for above interpolation
        [HarmonyPatch(typeof(dds3UnitObjectBasic), "dds3AddUnitObjectBasic")]
        public static class InstanceStorage
        {
            public static void Postfix(ref dds3Basic_t __result)
            {
                //crucialinstances.Add(__result);

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


        //this patches a variety of methods that need to be locked to 30fps
        [HarmonyPatch]
        public static class LockTo30FPS
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {

                //philosophy is to patch as little as possible here, patching multiple things or trying to create new calls often goes wrong


                //main logic calls from kernel update loop
                yield return AccessTools.Method(typeof(dds3KernelCore), "dds3ProcessMain");
                yield return AccessTools.Method(typeof(dds3KernelCore), "ExecUnloadUnusedAssets");


                //fixes particles flickering
                yield return typeof(GraphicManager.CommonObject).GetMethod("LateUpdate");


                //fixes demon model flickering
                yield return AccessTools.Method(typeof(ModelHD), "CallLateUpdate");


                //fix flickering bloom
                yield return AccessTools.Method(typeof(PostProcessStackV2Manager), "Update");


                //doesnt completely fix flickering but makes it a lot less drastic
                //I imagine the real way to completely remove flickering and have this be 100% correct would be to patch PostBlur.OnRenderImage to preserve the accumBlur created at 30fps
                yield return AccessTools.Method(typeof(PostBlur), "Update");
                yield return AccessTools.Method(typeof(effBlur_Filter), "dds3Effect_UpdateBigin");
                yield return AccessTools.Method(typeof(effBlur_Filter), "dds3Effect_UpdateEnd");
                
                yield return AccessTools.Method(typeof(RippleEffect), "Update");


                //fix scrolling text speed
                yield return AccessTools.Method(typeof(ScrollText), "Update");


            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = Determine30fps();

            }


            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    Msg(__exception.ToString());
                }
                return null;
            }

        }



        //currently unused, should probably stay that way...
        //this is the opposite of locking to 30 - it creates new calls for the real framerate with same variables as the real calls of last real frame
        //potentially useful in a situation where the original call site is limited to 30
        public static List<Tuple<dynamic, MethodBase, object[]>> calls = new List<Tuple<dynamic, MethodBase, object[]>>();
        //[HarmonyPatch]
        public static class BringToTarget
        {
            public static void Prefix(ref object[] __args, MethodBase __originalMethod, ref dynamic __instance)
            {
                if (Determine30fps())
                {
                    calls.Add(new Tuple<dynamic, MethodBase, object[]>(__instance, __originalMethod, __args));
                }
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
                __instance.Axis = ((__instance.Axis / Time.deltaTime) / 30);

            }

        }


        /*
         * This whole section isnt in the current release. It was intended to record any input checks created in the last frame, 
         * then check them again in the next frames leading up to the real frame and record the result.
         * Then when it became a new frame again, tell the game that those inputs were all pressed as if they were all pressed at the same time on a 30fps frame.
         * 
         * For some reason it doesnt work properly though. Most of it does, but a lot of one shot inputs dont.
         * The main input checking methods are the ones like DDS3_PADCHECK_PRESS in dds3padmanager.
         * Also be aware of the methods that contain the word chkCommonInput. I feel like I couldnt get it to fully work because I was missing something to do with those maybe.
         * Including those chkCommonInput methods in the input recording patch below, and changing result recording to dynamic instead of bool to allow for uint returns, does improve things a little, but isnt a fix.
        */


        //list of real calls made on last real frame
        public static List<Tuple<MethodBase, object[]>> lastframepresschecks = new List<Tuple<MethodBase, object[]>>();

        //list of lists of additional calls that returned true on each new frame
        public static List<List<Tuple<MethodBase, object[]>>> inputretainpress = new List<List<Tuple<MethodBase, object[]>>>();


        //This creates input calls from the last real frame
        [HarmonyPatch(typeof(dds3PadManager), "dds3PadUpdate")]
        public static class MainInputPatch
        {
            public static void Postfix()
            {

                //if new frame, begin cache input results
                if (!Determine30fps())
                {
                    inputretainpress.Add(new List<Tuple<MethodBase, object[]>>());

                    //evaluate every check that was in last frame
                    foreach (Tuple<MethodBase, object[]> x in lastframepresschecks)
                    {

                        x.Item1.Invoke(null, x.Item2);

                    }
                }
            }
        }


        //padcheck trig is for one shot
        //padcheck press is for constant
        //Never checked what rep is for, probably repeating, but I dont know how thats differing than padcheck press.

        //this patches every input method and forces it to return true if it were true on a fake frame
        //that way input runs at actual framerate but is kinda condensed down to 30 so the game logic sees it all
        [HarmonyPatch]
        public static class InputCallPatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (MethodBase m in typeof(dds3PadManager).GetMethods())
                {
                    //returns the input check methods
                    if (m.Name.StartsWith("DDS3_"))
                    {
                        //Msg(m.Name + " patched");
                        yield return m;
                    }
                }

            }

            public static void Prefix(MethodBase __originalMethod, object[] __args, ref bool __runOriginal, ref bool __result)
            {

                //if original frame, check if input was pressed on fake frames and return if true
                if (Determine30fps())
                {
                    Tuple<MethodBase, object[]> res = new Tuple<MethodBase, object[]>(__originalMethod, __args);

                    lastframepresschecks.Add(res);

                    foreach (List<Tuple<MethodBase, object[]>> framelist in inputretainpress)
                    {
                        foreach (Tuple<MethodBase, object[]> entry in framelist)
                        {
                            if (entry.Item1.Equals(res.Item1) && Enumerable.SequenceEqual(entry.Item2, res.Item2))
                            {

                                __runOriginal = false;
                                __result = true;

                                //Msg($"found! {string.Join(", ", __args)}");

                                return;
                            }
                        }
                    }
                }

            }

            //this is definitely returning true when it should
            //the cause has to be some method in between the logic and this input check filtering out the result.

            public static void Postfix(MethodBase __originalMethod, object[] __args, ref bool __result)
            {

                //if input check was true then add it to the list to be pushed in next real frame
                if (!Determine30fps() && __result)
                {
                    //get newest frame input list and add input to it
                    inputretainpress[inputretainpress.Count - 1].Add(new Tuple<MethodBase, object[]>(__originalMethod, __args));
                }

            }

            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    Msg(__exception.ToString());
                }
                return null;
            }
        }



        #endregion


        #region MISCELLANEOUS PATCHES


        //patch to ensure battle demons and other animations that rely on this have unlocked animation framerates
        [HarmonyPatch(typeof(FrameAnime), "LateUpdate")]
        public static class FrameAnimePatch
        {
            public static void Postfix(ref FrameAnime __instance, ref bool __runOriginal)
            {
                __instance.time = 1f / CurrentFramerate;

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



        //start of a patch to fix blur, couldnt get it to work, I dont know a lot about unity render textures

        /*
        [HarmonyPatch(typeof(PostBlur), "OnRenderImage")]
        public static class renderfix
        {
            public static void Prefix()
            {
                if (!Determine30fps())
                {
                    //dds3KernelDraw.Postblur_refresh = false;
                    stfMain.Blur.refresh_Blit = false;
                }
            }
        }*/

        #endregion


        /// <summary>
        /// PATCHES END HERE
        /// </summary>


    }
}


