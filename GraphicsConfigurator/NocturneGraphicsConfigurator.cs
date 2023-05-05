
//c# stuff
using System.Reflection;
using System.IO;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

//modding/unity stuff
using MelonLoader;
using static MelonLoader.MelonLogger;
using UnityEngine;
using HarmonyLib;
using UnityEngine.UI;

//game stuff
using Il2Cppbasic_H;
using Il2CppXRD773Unity;
using Il2Cpp;
using Il2Cppkernel_H;

[assembly: MelonInfo(typeof(Nocturne_Graphics_Configurator.NocturneGraphicsConfigurator), "Nocturne Graphics Configurator", "1.7", "vvdashdash")]
[assembly: MelonGame(null, "smt3hd")]

namespace Nocturne_Graphics_Configurator
{
    public class NocturneGraphicsConfigurator : MelonMod
    {

        /// <summary>
        /// 
        /// The framerate:
        /// 
        /// This mod fundamentally works by putting the game logic in a 30fps fixedupdate, and interpolating objects around on "fake" frames.
        /// 
        /// As its using interpolation, I have to know both the A and B positions of objects, so I'm retaining all objects 1 30-fps frame behind where they actually should be, and interpolating to their intended destination.
        /// And because the interpolation is generally applied at all times, to everything, without consideration for teleportations, I have to patch every teleportation method and tell the game not to interpolate that frame.
        /// 
        /// The game logic is run within a 30fps fixedupdate, and using various bools set by the loops at different points or by patched game methods, the interpolation is controlled.
        /// 
        /// There are also a lot of random small fixes and logic tweaks for many visual elements that for some reason have to run at the real framerate, mainly rendering/animation elements.
        /// 
        /// 
        /// 
        /// The ultrawide/resolution override:
        /// 
        /// This game has a ton of random rendertextures and scripts that force or read screen resolution, really seems like overkill, and a mess.
        /// 
        /// Im generally telling these scripts and methods that the screen resolution is either 1080p (so it doesnt try to limit its own aspect ratio or break scaling) or that its the overridden resolution (so that it renders higher)
        /// 
        /// Im then stretching or moving a bunch of the UI/2D objects to fit the new aspect, as well as adding my own black bar objects to mask off menus that just cant reasonably be expanded.
        /// 
        /// 
        /// 
        /// Final notes:
        /// 
        /// This mod's code is a mess, evidently. A lot of the development was just throwing things at the wall and seeing what worked, or taking the easiest/fastest possible route.
        /// If you want to continue this project or learn about it, message me on discord. I'd be happy to explain anything.
        /// 
        /// 
        /// </summary>


        //all of the config settings

        //settings for framerate interpolation/speedhack
        public static MelonPreferences_Category ModSettings;
        public static MelonPreferences_Entry<int> Framerate;
        public static MelonPreferences_Entry<float> SpeedhackSpeed;
        public static MelonPreferences_Entry<int> VSyncMode;
        public static MelonPreferences_Entry<bool> CustomFramerateOnLaunch;

        //settings for resolution override
        public static MelonPreferences_Category ModOverrideSettings;
        public static MelonPreferences_Entry<int> ResolutionOverrideX;
        public static MelonPreferences_Entry<int> ResolutionOverrideY;
        public static MelonPreferences_Entry<int> ShadowOverrideX;
        public static MelonPreferences_Entry<int> ShadowOverrideY;

        //key bindings
        public static MelonPreferences_Category ModKeySettings;
        public static MelonPreferences_Entry<string> FramerateToggleKey;
        public static MelonPreferences_Entry<string> SpeedhackToggleKey;
        public static MelonPreferences_Entry<string> UIToggleKey;

        //general graphics options
        public static MelonPreferences_Category ModGraphicsSettings;
        public static MelonPreferences_Entry<bool> ExclusiveFullscreenWhenFullscreen;
        public static MelonPreferences_Entry<bool> BloomEnabled;




        //internal bools for those variables
        public static bool speedhack = false;
        public static bool Unlock = false;
        public static bool UIOn = true;
        public static int CurrentFramerate = 30;
        

        //global objects
        public static GameObject ui;
        public static GameObject uicamera;
        public static PostProcessStackV2Manager ppsv2;
        public static GameObject Trackers;



        //stuff for aspect ratio support
        public static bool useresolutionoverride = false;
        public static float aspectratio;
        public static Vector3 scaledifference;
        public static Vector3 positionaloffsetlocal;
        public static float buffer;
        public static float bufferh;


        public static Vector3 placehold = new Vector3(0, 9999, 0);


        //for some reason some UI elements dont want to fully hide with just the layer technique, so I'm splitting this into two techniques
        //layer performance is better and code is cleaner though, so if you know why this is then do tell me

        //whitelist of UI objects to hide/show
        public static List<string> uiremoves = new List<string>() { "talkUI", "fieldUI", "bparty(Clone)", "bmenuset(Clone)", "fldName", "bturnset(Clone)", "bannounce(Clone)" };
        //whitelist of UI objects to hide and show but using layer 31 and layermask instead, to prevent conflicting with existing canvasgroups
        public static List<string> uiremoveslayerhide = new List<string>() { "buttonguide01" };



        //allows debug key to be pressed, used to be used much more extensively but 0.6+ hot reload removed a lot of the need for it
        public static bool MODDEBUGMODE = false;

        //debug value to show/hide interpolation target objects
        public static bool targsvis = false;


        //this is to cover up things behind/besides menus and stuff for aspect ratio
        public static GameObject customblackbox;
        public static GameObject customblackboxL;
        public static GameObject customblackboxR;
        public static GameObject customblackboxU;
        public static GameObject customblackboxD;


        #region MELONLOADER INIT + REGULAR FUNCTIONS

        //Config and initialization
        public override void OnInitializeMelon()
        {

            ModSettings = MelonPreferences.CreateCategory("FRAMERATE AND SPEEDHACK");
            ModOverrideSettings = MelonPreferences.CreateCategory("RESOLUTION AND SHADOW");
            ModKeySettings = MelonPreferences.CreateCategory("TOGGLE BINDINGS");
            ModGraphicsSettings = MelonPreferences.CreateCategory("EXTRA GRAPHICS");


            Framerate = ModSettings.CreateEntry("Framerate", 60);
            SpeedhackSpeed = ModSettings.CreateEntry("SpeedhackSpeed", 2.0f);
            CustomFramerateOnLaunch = ModSettings.CreateEntry("CustomFramerateOnLaunch", true);
            VSyncMode = ModSettings.CreateEntry("VSyncModeWhenCustomFramerate", 1);


            BloomEnabled = ModGraphicsSettings.CreateEntry("Bloom", true);
            ExclusiveFullscreenWhenFullscreen = ModGraphicsSettings.CreateEntry("ExclusiveFullscreen", false);


            ResolutionOverrideX = ModOverrideSettings.CreateEntry("ResolutionOverrideWidth", 0);
            ResolutionOverrideY = ModOverrideSettings.CreateEntry("ResolutionOverrideHeight", 0);
            ShadowOverrideX = ModOverrideSettings.CreateEntry("ShadowMapOverrideWidth", 0);
            ShadowOverrideY = ModOverrideSettings.CreateEntry("ShadowMapOverrideHeight", 0);


            FramerateToggleKey = ModKeySettings.CreateEntry("FramerateToggleKey", "f11");
            SpeedhackToggleKey = ModKeySettings.CreateEntry("SpeedhackToggleKey", "f10");
            UIToggleKey = ModKeySettings.CreateEntry("UIToggleKey", "f9");



            string path = "NocturneGraphicsConfiguratorConfig.cfg";

            ModSettings.SetFilePath(path, autoload: true, printmsg: false);

            ModOverrideSettings.SetFilePath(path, autoload: true, printmsg: false);

            ModGraphicsSettings.SetFilePath(path, autoload: true, printmsg: false);

            ModKeySettings.SetFilePath(path, autoload: true, printmsg: false);


            if (File.Exists(path))
            {
                LoggerInstance.Msg("Mod config loaded.");
                ModSettings.LoadFromFile(false);
                ModOverrideSettings.LoadFromFile(false);
                ModGraphicsSettings.LoadFromFile(false);
                ModKeySettings.LoadFromFile(false);
            }
            else
            {
                LoggerInstance.Msg("No config was found - mod config created in root directory.");
                ModSettings.SaveToFile(false);
                ModOverrideSettings.SaveToFile(false);
                ModGraphicsSettings.SaveToFile(false);
                ModKeySettings.SaveToFile(false);
            }


            Unlock = CustomFramerateOnLaunch.Value;

            if (Unlock)
            {
                CurrentFramerate = Framerate.Value;
                
            }

            currentFrameTime = Time.realtimeSinceStartup;
            MelonCoroutines.Start(WaitForNextFrame());


            useresolutionoverride = ResolutionOverrideX.Value != 0 && ResolutionOverrideY.Value != 0;
        }


        public static bool DidForce30 = false;
        public static bool Force30 = false;
        public static bool ForceBackUp = false;

        //regular lateupdate function, used for constant mod input and configuration, not game patching
        public override void OnLateUpdate()
        {
            if (!Application.isFocused)
            {
                return;
            }

            if (Application.targetFrameRate != CurrentFramerate)
            {
                Application.targetFrameRate = CurrentFramerate;
            }


            if (Unlock)
            {
                QualitySettings.vSyncCount = VSyncMode.Value;
            }


            if (Input.GetKeyDown(FramerateToggleKey.Value) || Force30 || ForceBackUp)
            {
                Unlock = !Unlock;

                if (Force30)
                {
                    Unlock = false;
                }
                if (ForceBackUp)
                {
                    Unlock = true;
                }

                //makes sure trackers are updated
                InterpolationStage1(false);
                InterpolationStage1(true);


                currentFrameTime = Time.realtimeSinceStartup;

                if (Unlock)
                {
                    CurrentFramerate = Framerate.Value;

                    LoggerInstance.Msg("Framerate mod enabled (" + CurrentFramerate.ToString() + " FPS target).");

                }
                else
                {
                    CurrentFramerate = 9999;
                    SteamScreen.SetFrameRate();

                    LoggerInstance.Msg("Framerate mod disabled.");
                }

            }

            ForceBackUp = false;
            Force30 = false;


            if (Input.GetKeyDown(UIToggleKey.Value))
            {

                UIOn = !UIOn;

                LoggerInstance.Msg("UI set to " + UIOn.ToString());

            }

            if (Input.GetKeyDown(SpeedhackToggleKey.Value))
            {

                speedhack = !speedhack;

                LoggerInstance.Msg("Speedhack set to " + speedhack.ToString());

                if (speedhack)
                {
                    Time.fixedDeltaTime = 1f / (SpeedhackSpeed.Value * 30f);
                }
                else
                {
                    Time.fixedDeltaTime = 1f / 30f;
                }

            }


            if (MODDEBUGMODE)
            {
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    Msg("debug key pressed");

                    Msg("interpolation target visualizers toggled");
                    targsvis = !targsvis;

                    unitobjectdetails();
                }

                //debug visualise interpolation objects
                foreach (KeyValuePair<dds3Basic_t, GameObject> x in interpolationobjlist)
                {
                    if (x.Value != null && x.Value.GetComponent<MeshRenderer>() != null)
                    {
                        x.Value.GetComponent<MeshRenderer>().enabled = targsvis;
                    }
                }
            }


            //done this way so game can still disable bloom if it wants
            if (ppsv2 != null && !BloomEnabled.Value)
            {
                ppsv2.bloom.active = false;
            }
            

            updateui();

        }


        //scans for and hides UI according to above lists
        public static void updateui()
        {

            if (ui == null)
            {
                ui = GameObject.Find("Canvas_UI");
            }

            if (uicamera == null)
            {
                uicamera = GameObject.Find("UI Camera");
            }
            
            if (ui != null)
            {

                int lyr = 31;
                //constantly finds strings because objects get added/removed often
                foreach (string x in uiremoveslayerhide)
                {
                    var find = ui.transform.Find(x);
                    if (find != null)
                    {
                        find.gameObject.layer = lyr;
                        if (find.gameObject.GetComponent<Canvas>() == null)
                        {
                            find.gameObject.AddComponent<Canvas>();
                        }
                    }
                }


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

                        Component.alpha = UIOn ? 1 : 0;
                    }
                }


                if (uicamera != null)
                {
                    if (UIOn)
                    {
                        uicamera.GetComponent<Camera>().cullingMask |= (1 << lyr);
                    }
                    else
                    {
                        uicamera.GetComponent<Camera>().cullingMask &= ~(1 << lyr);
                    }
                }
            }



            //exclusive fullscreen force
            //this has a weird issue where the screen flickers an old frame for a second sometimes... seems more like a unity problem than a game problem, but Im not sure..

            if (ExclusiveFullscreenWhenFullscreen.Value)
            {
                //this is the internal render scale, either overridden by myself or just what the game's quality scale is set to
                RenderTexture sclchg = GlobalData.kernelObject.scaleChangeTexture;

                if ((Screen.fullScreen && (Screen.fullScreenMode != FullScreenMode.ExclusiveFullScreen || (Screen.width != sclchg.width || Screen.height != sclchg.height))))
                {
                    Screen.SetResolution(ResolutionOverrideX.Value, ResolutionOverrideY.Value, FullScreenMode.ExclusiveFullScreen);
                }
            }

            //+ adjust objects transforms on screen for aspect ratio

            if (useresolutionoverride)
            {
                aspectratio = ResolutionOverrideX.Value / (float)ResolutionOverrideY.Value;
                scaledifference = new Vector3(Mathf.Max(aspectratio / (1920f / 1080f),1f), Mathf.Max((1920f / 1080f) / aspectratio,1f), 1);

                buffer = (1920f / 2f * (1f - scaledifference.x));
                bufferh = (1080f / 2f * (1f - scaledifference.y));



                //always enforce aspect ratio and correct resolution

                if (Screen.fullScreen && !ExclusiveFullscreenWhenFullscreen.Value)
                {
                    int cdisplay = SteamScreen.NewInfo.DisplayNo;

                    int maxWidth = Display.displays[cdisplay].systemWidth;
                    int maxHeight = Display.displays[cdisplay].systemHeight;

                    int desiredHeight = (int)(maxWidth / aspectratio);
                    int desiredWidth = maxWidth;

                    if (desiredHeight > maxHeight)
                    {
                        desiredHeight = maxHeight;
                        desiredWidth = (int)(desiredHeight * aspectratio);
                    }

                    if (Screen.width != desiredWidth || Screen.height != desiredHeight)
                    {
                        Screen.SetResolution(desiredWidth, desiredHeight, FullScreenMode.FullScreenWindow);
                    }

                }
                else
                {
                    if (Screen.width != (int)(Screen.height * aspectratio))
                    {
                        Screen.SetResolution((int)(Screen.height * aspectratio), Screen.height, false);
                    }
                }



                //dont mess with gameobjects beyond this point if not ready
                if (!((!Unlock || boolforlateframe) && kernel != null && kernel.isActiveAndEnabled))
                {
                    return;
                }

                //compass
                if (GlobalData.kernelObject.enemyUI != null)
                {
                    GlobalData.kernelObject.enemyUI.transform.localPosition = Vector3.Scale(GlobalData.kernelObject.enemyUI.transform.localPosition, scaledifference);
                }

                //field name indicator
                if (GlobalData.kernelObject.areaUI != null)
                {
                    GlobalData.kernelObject.areaUI.transform.localPosition = Vector3.Scale(GlobalData.kernelObject.areaUI.transform.localPosition, scaledifference);
                }

                //field gradient filter
                if (GlobalData.kernelObject.fldSky != null)
                {
                    GlobalData.kernelObject.fldSky.transform.localScale = scaledifference;
                }

                


                //offset the menus to be center screen


                Vector3 posoffset = new Vector3(-(1920/2), 540, 0);


                //title logo and title menu, etc
                //not sure where these are referenced, so I'm just finding them for now
                GameObject title = GameObject.Find("titleUI(Clone)");
                if (title != null)
                {
                    title.transform.localPosition = posoffset;
                }

                if (GameObject.Find("RdLogoUI(Clone)") != null)
                {
                    GameObject.Find("RdLogoUI(Clone)").transform.localPosition = posoffset;
                }


                //area intro title texts
                if (fldTitle.tit_obj != null)
                {
                    fldTitle.tit_obj.transform.localScale = new Vector3(scaledifference.x, 1, 1);

                    fldTitle.tit_obj.transform.localPosition = new Vector3(-960*scaledifference.x, 720f / scaledifference.y, 0);

                    float invscale = 1f / scaledifference.x;

                    //stretch all of them and then unstretch text
                    for (int i = 0; i < fldTitle.tit_obj.transform.childCount; i++)
                    {
                        for (int x = 0; x < fldTitle.tit_obj.transform.GetChild(i).childCount; x++)
                        {
                            if (fldTitle.tit_obj.transform.GetChild(i).GetChild(x).name.Contains("areaname"))
                            {
                                fldTitle.tit_obj.transform.GetChild(i).GetChild(x).transform.localScale = new Vector3(invscale, 1, 1);
                            }
                        }
                    }
                }


                //main pause stuff
                if (GlobalData.kernelObject.campObjUI != null)
                {
                    GlobalData.kernelObject.campObjUI.transform.GetParent().localPosition = posoffset;
                }

                //automap
                if (GlobalData.kernelObject.autoMapObj != null)
                {
                    GlobalData.kernelObject.autoMapObj.transform.localPosition = posoffset;
                }


                //saveload screen
                if (slMain._saveUI != null)
                {
                    slMain._saveUI.transform.localPosition = posoffset;
                }



                //magatama screen

                if (cmpInitDH.DHeartsObj != null)
                {
                    cmpInitDH.DHeartsObj.transform.localPosition = posoffset;
                }


                //battle result screen
                if (nbResultProcess.oResult_UI != null)
                {
                    nbResultProcess.oResult_UI.transform.localPosition = posoffset;
                }



                //battle gui
                if (nbMainProcess.oBattle_UI != null)
                {
                    float y1 = -(252f * scaledifference.y);
                    nbMainProcess.oBattle_UI[1].transform.localPosition = new Vector3(nbMainProcess.oBattle_UI[1].transform.localPosition.x, y1, 0);
                    nbMainProcess.oBattle_UI[0].transform.localPosition = new Vector3(nbMainProcess.oBattle_UI[0].transform.localPosition.x, y1+66, 0);

                    //[1] y is -252f
                    //[0] y is -186f

                    //differ is 66

                    //(-904.0, -6.0, 0.0)
                }


                if (nbMainProcess.oTargetMark2D != null)
                {
                    //target cursors
                    foreach (GameObject trg in nbMainProcess.oTargetMark2D)
                    {
                        if (trg != null && trg.active)
                        {
                            trg.transform.localPosition = Vector3.Scale(trg.transform.localPosition, scaledifference);
                        }
                    }
                }



                Vector3 submenuoffsetL = new Vector3(((1920 / 2f) * (1f - scaledifference.x)) - 4, -540 * (1f - scaledifference.y), 0);
                Vector3 submenuoffsetR = new Vector3(1920 - (1920 / 2f) * (1f - scaledifference.x) + 4, -540 * (1f - scaledifference.y), 0);
                //cathedral, fountain, junk shop, etc, those kinds of menus

                if (fclUI.fclObj != null)
                {

                    //options present in all of these
                    if (fclUI.fclObj.transform.FindChild("institutionUI") != null)
                    {
                        fclUI.fclObj.transform.FindChild("institutionUI").transform.localPosition = submenuoffsetL;
                    }


                    switch (fclUI.fclObj.name)
                    {

                        //cathedral
                        case string a when a.Contains("combUI"):

                            if (fclUI.fclObj.transform.FindChild("combgcolor") != null)
                            {
                                //-4 -4 0
                                fclUI.fclObj.transform.FindChild("combgcolor").transform.localPosition = submenuoffsetL + new Vector3(-4, 4, 0);
                                fclUI.fclObj.transform.FindChild("combgcolor").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            if (fclUI.fclObj.transform.FindChild("combmenu/combase") != null)
                            {
                                //-4 -262 0
                                fclUI.fclObj.transform.FindChild("combmenu/combase").transform.localPosition = submenuoffsetL + new Vector3(-4, -262 * scaledifference.y, 0);
                                fclUI.fclObj.transform.FindChild("combmenu/combase").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            if (fclUI.fclObj.transform.FindChild("comtitle") != null)
                            {
                                //-4 -38 0
                                fclUI.fclObj.transform.FindChild("comtitle").transform.localPosition = submenuoffsetL + new Vector3(-4, -38 * scaledifference.y, 0);
                            }

                            if (fclUI.fclObj.transform.FindChild("combmenu/comname") != null)
                            {
                                //1126 -146 0
                                fclUI.fclObj.transform.FindChild("combmenu/comname").transform.localPosition = new Vector3(1126 * scaledifference.x, -146 * scaledifference.y, 0);
                            }

                            //154 -118 0
                            if (fclUI.fclObj.transform.FindChild("combmenu/comdname01") != null)
                            {
                                fclUI.fclObj.transform.FindChild("combmenu/comdname01").transform.localPosition = submenuoffsetL + new Vector3(154, -118 * scaledifference.y, 0);
                            }

                            break;



                        //fountain
                        case string a when a.Contains("recovUI"):


                            if (fclUI.fclObj.transform.FindChild("rcvbgcolor") != null)
                            {
                                //-4 -4 0
                                fclUI.fclObj.transform.FindChild("rcvbgcolor").transform.localPosition = submenuoffsetL + new Vector3(-4, 4, 0);
                                fclUI.fclObj.transform.FindChild("rcvbgcolor").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }


                            //-4 -236 0
                            if (fclUI.fclObj.transform.FindChild("rcvlist/rcvbase") != null)
                            {
                                //-4 -236 0
                                fclUI.fclObj.transform.FindChild("rcvlist/rcvbase").transform.localPosition = submenuoffsetL + new Vector3(0, -236 * scaledifference.y, 0);
                                fclUI.fclObj.transform.FindChild("rcvlist/rcvbase").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }


                            if (fclUI.fclObj.transform.FindChild("rcvmoney") != null)
                            {
                                //1924 -58 0
                                fclUI.fclObj.transform.FindChild("rcvmoney").transform.localPosition = submenuoffsetR + new Vector3(0,-58*scaledifference.y,0);
                            }

                            //1924 -134 0
                            if (fclUI.fclObj.transform.FindChild("rcvfee") != null)
                            {
                                fclUI.fclObj.transform.FindChild("rcvfee").transform.localPosition = submenuoffsetR + new Vector3(0, -134 * scaledifference.y, 0);
                            }


                            if (fclUI.fclObj.transform.FindChild("rcvtitle") != null)
                            {
                                //-4 -38 0
                                fclUI.fclObj.transform.FindChild("rcvtitle").transform.localPosition = submenuoffsetL + new Vector3(-4, -38 * scaledifference.y, 0);
                            }


                            break;

                        //junk shop
                        case string a when a.Contains("shopUI"):

                            if (fclUI.fclObj.transform.FindChild("shopmoney") != null)
                            {
                                //1924 -58 0
                                fclUI.fclObj.transform.FindChild("shopmoney").transform.localPosition = submenuoffsetR + new Vector3(0, -58 * scaledifference.y, 0);
                            }

                            if (fclUI.fclObj.transform.FindChild("shoptitle") != null)
                            {
                                //-4 -38 0
                                fclUI.fclObj.transform.FindChild("shoptitle").transform.localPosition = submenuoffsetL + new Vector3(-4 * scaledifference.x, -38 * scaledifference.y, 0);
                            }


                            if (fclUI.fclObj.transform.FindChild("shopbgcolor") != null)
                            {
                                //-4 -4 0
                                fclUI.fclObj.transform.FindChild("shopbgcolor").transform.localPosition = submenuoffsetL + new Vector3(-4, 4, 0);
                                fclUI.fclObj.transform.FindChild("shopbgcolor").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            if (fclUI.fclObj.transform.FindChild("shopbase") != null)
                            {
                                //-4 -236 0
                                fclUI.fclObj.transform.FindChild("shopbase").transform.localPosition = submenuoffsetL + new Vector3(0, -236 * scaledifference.y, 0);
                                fclUI.fclObj.transform.FindChild("shopbase").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            //1924 -134 0
                            if (fclUI.fclObj.transform.FindChild("shopfee") != null)
                            {
                                fclUI.fclObj.transform.FindChild("shopfee").transform.localPosition = submenuoffsetR + new Vector3(0, -134 * scaledifference.y, 0);
                            }

                            break;


                        //gem shop or whatever the fuck its called
                        case string a when a.Contains("ragUI"):

                            if (fclUI.fclObj.transform.FindChild("ragbgcolor") != null)
                            {
                                //-4 -4 0
                                fclUI.fclObj.transform.FindChild("ragbgcolor").transform.localPosition = submenuoffsetL + new Vector3(-4, 4, 0);
                                fclUI.fclObj.transform.FindChild("ragbgcolor").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            if (fclUI.fclObj.transform.FindChild("ragbase") != null)
                            {
                                //-4 -136 0
                                fclUI.fclObj.transform.FindChild("ragbase").transform.localPosition = submenuoffsetL + new Vector3(0, -136 * scaledifference.y, 0);
                                fclUI.fclObj.transform.FindChild("ragbase").transform.localScale = Vector3.Scale(scaledifference, new Vector3(1.1f, 1f, 1));
                            }

                            if (fclUI.fclObj.transform.FindChild("ragtitle") != null)
                            {
                                //-4 -38 0
                                fclUI.fclObj.transform.FindChild("ragtitle").transform.localPosition = submenuoffsetL + new Vector3(-4 * scaledifference.x, -38 * scaledifference.y, 0);
                            }

                            break;


                        //amala drum save terminal
                        case string a when a.Contains("terminalUI"):

                            if (fclUI.fclObj.transform.FindChild("tmnlcurrent") != null)
                            {
                                //1924 -38 0
                                fclUI.fclObj.transform.FindChild("tmnlcurrent").transform.localPosition = submenuoffsetR + new Vector3(4, -38 * scaledifference.y, 0);
                            }

                            if (fclUI.fclObj.transform.FindChild("menu_tmnl") != null)
                            {
                                //0 -176.0001 -4
                                fclUI.fclObj.transform.FindChild("menu_tmnl").transform.localPosition = submenuoffsetL + new Vector3(-4, -176f * scaledifference.y, 0);
                            }

                            if (fclUI.fclObj.transform.FindChild("tmnltitle") != null)
                            {
                                //-4 -38 0
                                fclUI.fclObj.transform.FindChild("tmnltitle").transform.localPosition = submenuoffsetL + new Vector3(-4 * scaledifference.x, -38 * scaledifference.y, 0);
                            }


                            break;


                    }

                    //Msg(fclUI.fclObj.name);

                    fclUI.fclObj.transform.localPosition = posoffset;
                }



                //config screen
                //dds3ConfigMain
                if (dds3ConfigMain.configObj != null)
                {
                    dds3ConfigMain.configObj.transform.localPosition = posoffset;
                }

                //main pause stuff
                if (GlobalData.kernelObject.campObjUI != null)
                {
                    GlobalData.kernelObject.campObjUI.transform.GetParent().localPosition = posoffset;
                }


                //no idea what this is for but it gets on screen sometimes so Im pushing it further away
                //2000 0 0
                if (GlobalData.kernelObject.BrighTextMesh != null)
                {
                    GlobalData.kernelObject.BrighTextMesh.transform.localPosition = new Vector3(2000*10,0,0);
                }


                //interact prompt
                //announceUI
                if (GlobalData.kernelObject.announceUI != null)
                {
                    //(960.0, -724.0, 0.0)

                    GlobalData.kernelObject.announceUI.transform.localPosition = new Vector3(960f * scaledifference.x, -724f * scaledifference.y, 0f);
                }



                //world map globe
                if (GlobalData.kernelObject.radarUI != null)
                {
                    //141 -956 0
                    GlobalData.kernelObject.radarUI.transform.localPosition = new Vector3(GlobalData.kernelObject.radarUI.transform.localPosition.x, -956 * scaledifference.y, 0);
                }


                bool puzzlehelp = false;
                //puzzleboy UI
                if (pbGame.oPB_UI != null)
                {
                    //background for puzzleboy
                    if (pbDraw.BGImage != null)
                    {
                        float maxc = Mathf.Max(scaledifference.x, scaledifference.y);
                        pbDraw.BGImage.transform.localScale = new Vector3(maxc, maxc, maxc);
                    }

                    pbGame.oPB_UI.transform.localPosition = posoffset;

                    if (pbGame.oPB_UI.transform.FindChild("pzlstage") != null)
                    {
                        //1494 -56 0
                        pbGame.oPB_UI.transform.FindChild("pzlstage").transform.localPosition = submenuoffsetR + new Vector3(-426, -56 * scaledifference.y, 0f);
                    }

                    if (pbGame.oPB_UI.transform.FindChild("pzlmenu") != null)
                    {
                        //90 -138 0
                        pbGame.oPB_UI.transform.FindChild("pzlmenu").transform.localPosition = submenuoffsetL + new Vector3(90, -138 * scaledifference.y, 0f);
                    }


                    puzzlehelp = pbGame.oPB_UI.transform.FindChild("pzlhelp") != null && pbGame.oPB_UI.transform.FindChild("pzlhelp").gameObject.active;

                }



                if (customblackbox != null)
                {
                    //if a menu is visible, then make sure the black background is visible
                    customblackbox.active = (GlobalData.kernelObject.campObjBG.active || GlobalData.kernelObject.autoMapObj.active ||
                        (title != null && title.active) || (slMain._saveUI != null && slMain._saveUI.gameObject.active) ||
                        (nbResultProcess.oResult_UI != null && nbResultProcess.oResult_UI.active) || puzzlehelp);


                    //make sure button hint is in bounds when in a locked off menu
                    if (GlobalData.kernelObject.buttonGuide != null)
                    {
                        if (!customblackbox.active)
                        {
                            GlobalData.kernelObject.buttonGuide.transform.localPosition = new Vector3(((1920 / 2f) * scaledifference.x) + 4, -514 * scaledifference.y, 0);
                        }
                        else
                        {
                            GlobalData.kernelObject.buttonGuide.transform.localPosition = new Vector3((1920 / 2f) + 4, -514, 0);
                        }

                        //new Vector3(1624, -514, 0)
                    }
                }

            }
        }


        //debug function to print information about tracked characters
        public static void unitobjectdetails()
        {
            Msg("these are the current unitobject setups");
            foreach (KeyValuePair<dds3Basic_t, GameObject> epic in interpolationobjlist)
            {
                Msg($"{epic.Key.name} (basic serial id: {epic.Key.serialID}, unity object id: {epic.Key.gameObject.GetInstanceID()}): {epic.Key.gameObject.transform.position},  {epic.Value.name} :  {epic.Value.transform.position}");
            }
        }


        #endregion


        #region CORE PATCHES AND PATCH FUNCTIONS



        /// <summary>
        /// the renderforcer uses this exact code literally ripped from a unity blog lmao https://blog.unity.com/technology/precise-framerates-in-unity
        /// though its been modified to set itself to 30 constantly, or some other object does that, Im not sure. Im not dealing with it, instead Im replacing it.
        /// ive modified that coroutine a little and added it here, started up in the init function
        /// </summary>


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


        float currentFrameTime;
        IEnumerator WaitForNextFrame()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                currentFrameTime += 1.0f / 30f;
                var t = Time.realtimeSinceStartup;
                var sleepTime = currentFrameTime - t - 0.01f;


                //fixes stalls when tabbed out
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

        public static bool ForceInterpolationOff = false;
        public static bool ForceExtraLoopCall = false;
        public static bool gamelogicrun = false;
        public static bool boolforafterdoneloop = false;
        public static bool boolforlateframe = false;


        /// <summary>
        /// This loop and nearby logic replaces the original call site of the game loop with a fixedupdate call
        /// It also logs where character objects are for interpolation, so they can be interpolated in the update loop postfix.
        /// </summary>

        public override void OnFixedUpdate()
        {
            
            if (!Application.isFocused || !Unlock)
            {
                return;
            }

            if (kernel != null && kernel.isActiveAndEnabled && kernel.initflag)
            {
                
                gamelogicrun = true;
                boolforafterdoneloop = true;
                boolforlateframe = true;

                //put everything back to last intended positions
                InterpolationStage1(true);

                ForceExtraLoopCall = false;
                ForceInterpolationOff = false;
                LerpProgress = 0f;


                //run the lateupdates for effects from a fixedupdate call site
                foreach (GraphicManager.CommonObject obj in lateupdatecommons)
                {
                    if (obj != null)
                    {
                        obj.LateUpdate();
                    }
                }

                lateupdatecommons.Clear();



                //
                //This was gonna be the patch for puzzleboy. It kinda works but not really, so disabled it. Feel free to try and finish this yourself
                //

                /*
                //this patches puzzleboy after the entire loop is done, for reasons explained at the PB patch
                if (PatchPuzzleBoy && pbDraw.BGImage != null)
                {
                    //character object
                    BasicTypeTrackAdd(new dds3Basic_t() { gameObject = pbModel.pModelObj.transform.GetChild(0).gameObject, name = "puzzlechar" }, true);

                    //background obj
                    BasicTypeTrackAdd(new dds3Basic_t() { gameObject = pbDraw.BGImage.gameObject, name = "puzzlebg" }, true);


                    *//*
                    //rotatable pieces
                    foreach (CommonMesh b in pbDraw.pMRBlk_Mesh)
                    {
                        if (b != null)
                        {
                            BasicTypeTrackAdd(new dds3Basic_t() { gameObject = b.gameObject }, true);
                            //Msg(b.gameObject.transform.GetSiblingIndex());
                        }
                    }

                    //movable pieces
                    foreach (CommonMesh b in pbDraw.pBlk_Mesh)
                    {
                        if (b != null)
                        {
                            BasicTypeTrackAdd(new dds3Basic_t() { gameObject = b.gameObject }, true);
                            Msg(b.gameObject.transform.GetSiblingIndex());
                        }
                    }
                    *//*

                    PatchPuzzleBoy = false;
                }
*/


                dds3KernelMain.m_dds3KernelMainLoop();



                //this is for camera event cuts/advancements

                if (fldCamera.fldCamNowEveCamera() != null)
                {
                    //Msg(fldCamera.fldCamNowEveCamera().name);
                    if (fldCamera.fldCamNowEveCamera().name != lastcamevename)
                    {
                        if (MODDEBUGMODE)
                        {
                            Msg("caught up event cam");
                        }
                        Catchup();
                    }
                    lastcamevename = fldCamera.fldCamNowEveCamera().name;
                }
                else
                {
                    if (lastcamevename != "")
                    {
                        if (MODDEBUGMODE)
                        {
                            Msg("caught up event cam reset");
                        }
                        Catchup();
                    }
                    lastcamevename = "";
                }


                //I am aware this is an ugly solution, but its the best I could do
                if (ForceExtraLoopCall)
                {
                    if (MODDEBUGMODE)
                    {
                        Msg("called ahead");
                    }

                    dds3KernelMain.m_dds3KernelMainLoop();

                    //..to compensate for the fact that I just called a frame ahead, again yes I know this is the uglest fucking thing ever
                    System.Threading.Thread.Sleep((int) (1000f / 30f));
                }


                //store intended transforms and place back to last fixed frame's transforms
                InterpolationStage1(false);


                if (ForceInterpolationOff)
                {
                    if (MODDEBUGMODE)
                    {
                        Msg("catchup");
                    }
                    //if catchup, immediately just put objects to where new logged intended positions are
                    InterpolationStage1(true);
                }

                gamelogicrun = false;

            }
        }


        //This returns true when a fixedupdate frame has just ran, not too extensively used anymore, originally was more crucial
        public static bool Determine30fps()
        {
            return (boolforafterdoneloop || speedhack || !Unlock);
        }


        //Same as above, but set forward one frame
        //mainly use this for lateupdate patches and/or patches that affect other objects
        public static bool DetermineLate30fps()
        {
            return (boolforlateframe || speedhack || !Unlock);
        }


        //call this when you want the game to force interpolation off for a frame and warp objects to where they should be
        public static void Catchup()
        {
            ForceInterpolationOff = true;
        }


        //call this if you want the game to call the game logic an extra time to 
        public static void CallAhead()
        {
            ForceExtraLoopCall = true;
        }



        //these are methods that create, destroy, set, get, and interpolate the objects that're given to them

        //this sets object positions and stores intended positions, or just sets object positions to intended
        public static void InterpolationStage1(bool SetToIntended = true)
        {

            //put everything back before the real loop so that any transformation ive done doesnt affect the game when it reads transforms
            //this fixes big issues in amala tunnels and puzzleboy 

            if (interpolationobjlist != null && interpolationobjlist.Count != 0)
            {
                HashSet<dds3Basic_t> args = new HashSet<dds3Basic_t>(interpolationobjlist.Keys);

                foreach (dds3Basic_t x in args)
                {
                    if (x != null && x.gameObject != null && interpolationobjlist[x] != null)
                    {
                        GameObject ch = x.gameObject;

                        if (ch.transform.childCount != 0 && x.gameObject.transform.GetChild(0).GetComponent<AnimCheckerBattle>() != null)
                        {
                            ch = x.gameObject.transform.GetChild(0).gameObject;
                        }

                        Transform dest = interpolationobjlist[x].transform;


                        if (SetToIntended && dest.position != placehold)
                        {
                            ch.transform.position = dest.position;
                            ch.transform.rotation = dest.rotation;
                        }
                        else
                        {
                            Vector3 np = ch.transform.position;
                            Quaternion nr = ch.transform.rotation;

                            ch.transform.position = dest.position;
                            ch.transform.rotation = dest.rotation;

                            dest.position = np;
                            dest.rotation = nr;
                        }

                    }
                }
            }
        }

        //this interpolates the objects in the list and sets lerpprogress
        public static void InterpolationStage2()
        {

            /*float delcomp = 30f / (1f / RealDeltaTime);
            LerpProgress = Mathf.Clamp(LerpProgress + delcomp, 0, 1);
            Msg(LerpProgress);*/

            //should be smoother even though it isnt correspondant to deltatime, because it attempts to spread the progress evenly
            LerpProgress = Mathf.Clamp(LerpProgress + Mathf.Clamp(1f / Mathf.Ceil((1f / RealDeltaTime) / 30f), 0, 0.5f), 0, 1);

            //interpolate unit objects
            if (interpolationobjlist != null)
            {
                HashSet<dds3Basic_t> args = new HashSet<dds3Basic_t>(interpolationobjlist.Keys);

                foreach (dds3Basic_t x in args)
                {
                    if (x != null && x.gameObject != null && interpolationobjlist[x] != null)
                    {
                        //interpolate first child if its a battle demon
                        //might need to add more cases to this

                        GameObject ch = x.gameObject;

                        if (ch.transform.childCount != 0 && x.gameObject.transform.GetChild(0).GetComponent<AnimCheckerBattle>() != null)
                        {
                            ch = x.gameObject.transform.GetChild(0).gameObject;
                        }

                        Transform dest = interpolationobjlist[x].transform;


                        //readded this threshold, might be a bad idea, dunno..
                        if (Vector3.Distance(ch.transform.position, dest.position) > 3.5)
                        {
                            ch.transform.position = dest.position;
                        }


                        if (fldCamera.fldCameraObj != null && ch.gameObject == fldCamera.fldCameraObj && fldCamera.fldCamNowEveCamera() == null && fldPlayer.fldPlayerObj != null)
                        {
                            Vector3 TargetCenterPos = new Vector3(fldPlayer.fldPlayerObj.transform.position.x, fldCamera.fldCameraObj.transform.position.y, fldPlayer.fldPlayerObj.transform.position.z);

                            //Msg(TargetCenterPos.y);

                            /*if (pbGame.oPB_UI != null)
                            {
                                //Msg("puzzleboy cam!");

                                //TargetCenterPos = pbModel.pModelObj.transform.GetChild(0).position + new Vector3(0, fldCamera.fldCameraObj.transform.position.y, 0);

                                TargetCenterPos = pbModel.pModelObj.transform.GetChild(0).position + new Vector3(0, fldCamera.fldCameraObj.transform.position.y, 0);
                            }
                            else if (fldPlayer.fldPlayerObj != null)
                            {
                                //Msg("field cam!");
                                TargetCenterPos = fldPlayer.fldPlayerObj.transform.position + new Vector3(0, fldCamera.fldCameraObj.transform.position.y, 0);
                            }*/

                            ch.transform.position = Vector3.Slerp((ch.transform.position - TargetCenterPos), (dest.transform.position - TargetCenterPos), LerpProgress) + TargetCenterPos;

                            ch.transform.rotation = Quaternion.Slerp(ch.transform.rotation, dest.rotation, LerpProgress);
                        }

                        else
                        {
                            ch.transform.position = Vector3.Lerp(ch.transform.position, dest.position, LerpProgress);
                            ch.transform.rotation = Quaternion.Slerp(ch.transform.rotation, dest.rotation, LerpProgress);
                        }

                    }

                }
            }
        }

        //this removes null gameobject trackers
        public static void ClearOutdatedTrackers()
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
                        Msg("duplicate found and destroyed");
                    }
                    continue;
                }

                //is basictype is valid then keep it
                if (x.Key != null && x.Key.gameObject != null && x.Value != null)
                {
                    newl.Add(x.Key, x.Value);
                    checker.Add(x.Key, x.Key.gameObject.GetInstanceID());
                    //x.Value.name = x.Key.name + "_frameVVmod";
                }

                //if basictype isnt valid then dont keep it, and if the tracker object exists still then delete it
                else if (x.Value != null)
                {
                    GameObject.Destroy(x.Value);
                }

            }

            if (MODDEBUGMODE && interpolationobjlist.Count != newl.Count)
            {
                Msg("removed " + (interpolationobjlist.Count - newl.Count).ToString() + " unitobject interpolator entries");
            }

            interpolationobjlist = newl;
        }

        //this adds a tracker to an object
        public static void BasicTypeTrackAdd(dds3Basic_t targ, bool bigvis = false, bool debugshape = true)
        {

            GameObject empty = null;
            if (MODDEBUGMODE && debugshape)
            {
                //Msg($"added a basictype for {targ.gameObject.name} - " + interpolationobjlist.Count);
                Msg("added a basictype - " + interpolationobjlist.Count);

                //creates a debug sphere 
                empty = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                empty.transform.localScale = Vector3.one * 0.2f;
                empty.GetComponent<MeshRenderer>().enabled = false;
                GameObject.Destroy(empty.GetComponent<SphereCollider>());

            }
            else
            {
                empty = new GameObject();
            }

            empty.transform.parent = Trackers.transform;
            empty.name = targ.name + "_target";

            if (bigvis)
            {
                empty.transform.localScale = Vector3.one * 25;
            }


            empty.transform.position = placehold;

            interpolationobjlist.Add(targ, empty);
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


        //this is for effects and particles, so their call site can be in fixedupdate, trust me its more performant

        public static HashSet<GraphicManager.CommonObject> lateupdatecommons = new HashSet<GraphicManager.CommonObject>();
        
        [HarmonyPatch]
        public static class ParticleDeferLateUpdate
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                //fixes particles flickering
                yield return typeof(GraphicManager.CommonObject).GetMethod("LateUpdate");
            }

            public static void Prefix(ref bool __runOriginal, ref GraphicManager.CommonObject __instance)
            {
                if (!Application.isFocused)
                {
                    __runOriginal = false;
                    return;
                }
                __runOriginal = gamelogicrun || !Unlock;
                if (!__runOriginal && DetermineLate30fps())
                {
                    lateupdatecommons.Add(__instance);
                }
            }
        }




        //List of objects to track
        public static Dictionary<dds3Basic_t, GameObject> interpolationobjlist = new Dictionary<dds3Basic_t, GameObject>();

        //did this because reading deltatime from a fixedupdate call very unhelpfully just gives you fixeddeltatime
        public static float RealDeltaTime = 0.0f;
        
        //lerp progress for object/value interpolation
        public static float LerpProgress = 0.0f;


        public static GameObject cam = null;
        //public static GameObject campcam = null;

        //for camera cuts, used by the fixedupdate
        public static string lastcamevename = "";


        //Patches the UPDATE loop of the game, so real framerate, not internal 30
        [HarmonyPatch(typeof(dds3DefaultMain), "Update")]
        public static class MainLoopPatch
        {
            public static void Postfix(ref dds3DefaultMain __instance)
            {
                //I set this for debugging/hot reload sometimes
                //MODDEBUGMODE = true;


                if (!Application.isFocused)
                {
                    return;
                }


                kernel = __instance;
                RealDeltaTime = Time.deltaTime;

                //fixes the entire keyboard missed input issue, I kid you not
                //it seems like this works by tricking the game into looking for keyboard inputs constantly instead of only on real frames
                SteamInputUtil.Instance.bAnyKeyDown = true;


                //trackers root object so Im not filling up the UE tree with trackers and confusing people
                if (Trackers == null)
                {
                    Trackers = new GameObject();
                    Trackers.name = "VVFramerateModObjects";
                }

                //cameras
                //shadowcams dont interpolate properly for some reason, I think their transforms are set at actual render framerate in OnPreRender..
                if (kernel != null)
                {
                    if (kernel.MainCamera != cam)
                    {
                        BasicTypeTrackAdd(new dds3Basic_t() { gameObject = kernel.MainCamera, name = "main_camera" }, false, false);
                        cam = kernel.MainCamera;
                    }



                    //just adding a tracker to compendium freecam doesnt (fully) work for some reason
                    //..so I cant be bothered. its such a minor thing that I dont care

                    /*if (campcam != GlobalData.kernelObject.campObjCamera)
                    {
                        campcam = GlobalData.kernelObject.campObjCamera;

                        BasicTypeTrackAdd(new dds3Basic_t() { gameObject = campcam, name = "status_camera" }, false, false);
                    }*/
                }


                if (!boolforafterdoneloop && boolforlateframe)
                {
                    boolforlateframe = false;
                }

                //if a new frame, interpolate everything between last original frame and intended current frame
                bool runinterpolation = !speedhack && Unlock && !ForceInterpolationOff;

                if (!Determine30fps())
                {
                    if (runinterpolation)
                    {
                        InterpolationStage2();
                    }
                }
                ClearOutdatedTrackers();



                //this is my duct tape solution to just limit puzzleboy and amala to 30.
                //if anyone's interested in actually making these run properly at unlocked, message me. I'll share what I've found out.

                dds3ProcessID_t pipeproc = dds3KernelCore.dds3SearchProcessName("PipeMain");

                dds3ProcessID_t pbdrawbase = dds3KernelCore.dds3SearchProcessName("pb-draw-mapbase");

                if ((pipeproc != null || pbdrawbase != null) && Unlock)
                {
                    DidForce30 = true;
                    Force30 = true;
                }
                if ((pipeproc == null && pbdrawbase == null) && !Unlock && DidForce30)
                {
                    DidForce30 = false;
                    ForceBackUp = true;
                }

                //Msg(fldPlayer.fldPlayerObjM == null);
                //player reflection, not sure if this logic is identical to original but whatever, I think this works everywhere without issue
                //not interpolating this with its own tracker because it can cause issues
                if (fldPlayer.fldPlayerObjM != null && fldPlayer.fldPlayerObjM.active)
                {
                    fldPlayer.fldPlayerObjM.transform.GetChild(0).gameObject.transform.position = fldPlayer.fldPlayerObj.transform.position;
                    fldPlayer.fldPlayerObjM.transform.GetChild(0).gameObject.transform.rotation = fldPlayer.fldPlayerObj.transform.rotation;
                    fldPlayer.fldPlayerObjM.transform.GetChild(0).gameObject.transform.Rotate(new Vector3(-90,0,0));
                }


                boolforafterdoneloop = false;

            }

        }



        //Patch to list crucial/character objects for above interpolation

        [HarmonyPatch(typeof(dds3UnitObjectBasic), "dds3AddUnitObjectBasic")]
        public static class InstanceStorageRegular
        {
            public static void Postfix(ref dds3Basic_t __result)
            {
                BasicTypeTrackAdd(__result);
            }
        }







        /*//puzzleboy objects

        //pbStartGame causes some kind of arbitrary failure and is only called one time, while patching pbInitGame is apparently too early for puzzle objects other than the character
        //..so Im using initgame to set a bool for one frame, which then grabs the objects late into the main loop

        public static bool PatchPuzzleBoy = false;

        //[HarmonyPatch(typeof(pbGame), "pbStartGame")]
        [HarmonyPatch(typeof(pbGame), "pbInitGame")]
        public static class InstangeStoragePuzzleBoy
        {
            public static void Postfix()
            {
                Catchup();
                CallAhead();
                PatchPuzzleBoy = true;
            }
        }*/


        //block outlines use unity line renderers that have their vertices updated instead of their actual object positions.
        //so... either interpolate the vertices (I did try that, it kinda works, but it isnt great), or more ideally, patch the actual block transformations with deltatime somehow.

        /*//interpolates blocks
        [HarmonyPatch(typeof(pbDraw), "drawBlock")]
        public static class InstangeStoragePuzzleBoy2
        {
            public static void Prefix()
            {
            }
        }*/




        //this is the best target ive found for a method that is called when a new area is loaded/entered
        //it patches doors and maybe other stuff I dont know

        [HarmonyPatch(typeof(fldTitle), "fldTitleMiniStart2")]
        public static class GimmickPatch
        {
            public static void Postfix()
            {
                if (fldFileResolver.FildMapObj != null && fldFileResolver.FildMapObj.GetComponent<GimmickObj>() != null)
                {
                    GimmickObj comp = fldFileResolver.FildMapObj.GetComponent<GimmickObj>();
                    foreach (GameObject obj in comp.DoorObj.Union(comp.HojiObj))
                    {
                        BasicTypeTrackAdd(new dds3Basic_t() { gameObject = obj, name = obj.name }, true);
                    }
                }
            }
        }




        //this fixes moments when the game actually puts something in the wrong place momentarily, with interpolation off or even in vanilla

        //this should only be used for methods that only occur one time and also really need it, because it calls the game loop a second time in a single frame and then waits 33ms.
        //...which isnt noticable if you do it once, but if you do it constantly for a while it looks awful


        [HarmonyPatch]
        public static class OutOfPlaceFix
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                //prevent opening/closing map jitter
                yield return AccessTools.Method(typeof(fldAutoMap), "fldAutoMapSeqEnd");
                yield return AccessTools.Method(typeof(fldAutoMap), "fldAutoMapSeqStart");

                //this one I'm using as a target for area/subarea loading just like minimap mod
                yield return AccessTools.Method(typeof(fldTitle), "fldTitleMiniStart2");


                foreach (MethodBase m in typeof(nbCameraSkill).GetMethods().Union(typeof(nbCameraBoss).GetMethods()))
                {
                    if (((m.Name.Contains("Cut") && m.Name.Contains("Init")) || (m.Name.Contains("Init") && !m.Name.Contains("End"))) && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }

                foreach (MethodBase m in typeof(nbCameraCommand).GetMethods())
                {
                    if (m.Name.Contains("_Set") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }

                foreach (MethodBase m in typeof(nbCameraNego).GetMethods())
                {
                    if (m.Name.Contains("_Init") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }


                //might be overkill but these are definitely used when battle chars teleport so why not
                foreach (MethodBase m in typeof(effBattleMisc).GetMethods())
                {
                    if ((m.Name.Contains("PopPosition") || m.Name.Contains("PopRotate")) && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }


                //used when battle changes states and stuff
                foreach (MethodBase m in typeof(nbCameraStartEnd).GetMethods())
                {
                    if (m.Name.Contains("_Init") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }

            }

            public static void Postfix()
            {
                CallAhead();
                Catchup();
            }
        }



        //this fixes methods that teleport but dont need as drastic a solution as calling the entire loop ahead.

        [HarmonyPatch]
        public static class TeleportationPatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {

                //for zoom-ins on doors/exits
                yield return AccessTools.Method(typeof(fldWap), "fldDoor_ExitCamera");

                //for miscellaneous camera reset cuts in field gameplay
                foreach (MethodBase m in typeof(fldCommand).GetMethods())
                {
                    if (m.Name.Contains("CAMERA") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }

                //these are constantly called sometimes so not a good idea

                /*//for miscellaneous camera reset cuts in field gameplay
                foreach (MethodBase m in typeof(fldCamera).GetMethods())
                {
                    if (m.Name.Contains("resetCam") && !m.IsSpecialName)
                    {
                        yield return m;
                    }
                }*/
            }

            public static void Postfix()
            {
                Catchup();
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
                if (Unlock)
                {
                    __instance.Axis = ((__instance.Axis / RealDeltaTime) / 30f);
                }
            }
        }


        #endregion


        #region VISUAL PATCHES



        //patches methods that need to be in tandem with the regular game logic at 30fps
        [HarmonyPatch]
        public static class LockTo30fps
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                //philosophy is to patch as little as possible in methods like these
                //I did try programatically patching every single update/lateupdate method and it caused a ton of errors..
                //and weeding through them all in a blacklist fashion would take a long time, there are too many
                //so a whitelist it is.


                //fix flickering bloom
                yield return AccessTools.Method(typeof(PostProcessStackV2Manager), "Update");


                //part of a fix for flickering accum blur processing, rest is at the bottom
                yield return AccessTools.Method(typeof(PostBlur), "Update");


                //not sure if this is needed or not, just added it anyway
                yield return AccessTools.Method(typeof(RippleEffect), "Update");

            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = Determine30fps();

            }
        }


        //this patches a variety of methods that need to be locked to 30fps but slightly later than game logic loop, for lateupdates and such
        [HarmonyPatch]
        public static class LockToLate30FPS
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                //fixes demon model flickering
                yield return AccessTools.Method(typeof(ModelHD), "CallLateUpdate");


                //fix scrolling text speed
                yield return AccessTools.Method(typeof(ScrollText), "Update");

                //fixes talk/options UI animation speed
                //I know this could look better but decompiling and/or modifying results for these just isnt worth it, the logic is all hardcoded/consts so it'd take real effort
                yield return AccessTools.Method(typeof(talkUI), "Update");
                yield return AccessTools.Method(typeof(talkChoice), "Update");

                yield return AccessTools.Method(typeof(UiHD), "LateUpdate");
            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = DetermineLate30fps();
            }
        }


        //patch to ensure battle demons and other animations that rely on this (like status view demons) have unlocked animation framerates
        [HarmonyPatch(typeof(FrameAnime), "LateUpdate")]
        public static class FrameAnimePatch
        {
            public static void Postfix(ref FrameAnime __instance, ref bool __runOriginal)
            {
                if (Unlock)
                {
                    __instance.time = RealDeltaTime;

                    //there might be a better way to do this but this works without issue I think
                    __instance.StepFrame();
                }
            }
        }



        //screen fade in/out patch

        public static Color fade1; //destination
        public static Color fade2; //last frame

        [HarmonyPatch(typeof(GfxManager), "LateUpdate")]
        public static class fadeinout
        {
            public static void Postfix(ref GfxManager __instance)
            {
                //__instance.tree_obj.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.clear;
                if (__instance.tree_obj != null && __instance.tree_obj.transform.childCount != 0)
                {
                    var obj = __instance.tree_obj.transform.GetChild(0);

                    var fade = obj.GetComponent<Image>();

                    if (!DetermineLate30fps())
                    {
                        fade.color = Color.Lerp(fade2, fade1, LerpProgress);
                    }
                    else
                    {
                        fade2 = fade1;
                        fade1 = fade.color;
                        fade.color = fade2;
                    }

                    //widescreen fit and setup
                    if (useresolutionoverride)
                    {

                        //make fade box huge so it always covers screen
                        obj.transform.localPosition = new Vector3(-10000,10000,0);
                        obj.transform.localScale = new Vector3(10000, 10000, 10000);


                        if (customblackbox == null && ui != null)
                        {
                            customblackbox = GameObject.Instantiate(obj.gameObject);
                            customblackbox.transform.SetParent(ui.transform);
                            customblackbox.transform.SetAsFirstSibling();
                            customblackbox.name = "VVDDmod_widescreenobject";

                            customblackbox.GetComponent<Image>().color = Color.black;

                            //covers sides of screen - overrides sorting so it can cut off edges.
                            customblackboxL = GameObject.Instantiate(customblackbox);
                            customblackboxR = GameObject.Instantiate(customblackbox);
                            customblackboxU = GameObject.Instantiate(customblackbox);
                            customblackboxD = GameObject.Instantiate(customblackbox);

                            customblackboxL.name = "LSide";
                            customblackboxR.name = "RSide";
                            customblackboxU.name = "USide";
                            customblackboxD.name = "DSide";

                            customblackboxL.transform.SetParent(customblackbox.transform);
                            customblackboxR.transform.SetParent(customblackbox.transform);
                            customblackboxU.transform.SetParent(customblackbox.transform);
                            customblackboxD.transform.SetParent(customblackbox.transform);

                            GameObject.Destroy(customblackbox.GetComponent<Image>());
                            customblackbox.GetComponent<Canvas>().overrideSorting = false;

                        }
                        else
                        {

                            float scl = (1f - Mathf.Max(1f / ((ResolutionOverrideX.Value/(float)ResolutionOverrideY.Value) / (1920f / 1080f)), 0))/2f;
                            float sclh = (1f - Mathf.Max(1f / ((1920f / 1080f) / (ResolutionOverrideX.Value / (float)ResolutionOverrideY.Value)), 0))/2f;


                            //ResolutionOverrideX.Value = 2560*2;
                            //ResolutionOverrideY.Value = 1080*2;

                            //ResolutionOverrideX.Value = 1920 * 2;
                            //ResolutionOverrideY.Value = 1080 * 2;

                            //ResolutionOverrideX.Value = 640;
                            //ResolutionOverrideY.Value = 480;


                            //fullscreen box
                            customblackbox.transform.localPosition = new Vector3(-1920 / 2f, 1080 / 2f, 0) + new Vector3(buffer, -bufferh, 0);
                            customblackbox.transform.localScale = scaledifference;

                            //L side
                            customblackboxL.transform.localPosition = new Vector3(0, 0, 0);
                            customblackboxL.transform.localScale = new Vector3(scl, 1, 0);

                            //R side
                            customblackboxR.transform.localPosition = new Vector3(1920 - (1920 * scl), 0, 0);
                            customblackboxR.transform.localScale = new Vector3(scl, 1, 0);

                            //U side
                            customblackboxU.transform.localPosition = new Vector3(0, 0, 0);
                            customblackboxU.transform.localScale = new Vector3(1, sclh, 0);

                            //D side
                            customblackboxD.transform.localPosition = new Vector3(0, -(1080 - (1080 * sclh)), 0);
                            customblackboxD.transform.localScale = new Vector3(1, sclh, 0);
                        }
                    }
                }
            }
        }


        //scales talkUI since it isnt static and isnt super easy to access from lateupdate

        [HarmonyPatch(typeof(talkUI), "Display")]
        public static class talkUIScale
        {
            public static void Postfix(ref talkUI __instance)
            {
                if (useresolutionoverride)
                {

                    if (customblackbox.active)
                    {
                        __instance.transform.localPosition = new Vector3(0, (-732 * scaledifference.y) - bufferh / 2f, 0);
                    }
                    else
                    {
                        __instance.transform.localPosition = new Vector3(0, (-732 * scaledifference.y), 0);
                    }

                    float invscale = 1f / scaledifference.x;

                    if (__instance.name.Contains("talk"))
                    {
                        __instance.transform.localScale = new Vector3(scaledifference.x, 1, 1);

                        Transform wind = __instance.transform.FindChild("talk_window");
                        for (int i = 0; i < wind.childCount; i++)
                        {
                            wind.GetChild(i).transform.localScale = new Vector3(invscale, 1, 1);
                        }
                        __instance.transform.FindChild("ok").localScale = new Vector3(invscale, 1, 1);
                    }


                    else if (__instance.name.Contains("name"))
                    {
                        if (!customblackbox.active)
                        {
                            __instance.transform.localPosition = new Vector3(183 - buffer, __instance.transform.localPosition.y, 0);
                        }
                        else
                        {
                            __instance.transform.localPosition = new Vector3(183 - buffer / 2f, __instance.transform.localPosition.y, 0);
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(talkChoice), "Update")]
        public static class talkChoiceScale
        {
            public static void Postfix(ref talkChoice __instance)
            {
                if (useresolutionoverride && DetermineLate30fps())
                {
                    //extended;
                    //(0.0, 322.0)
                    //[19:11:59.922] (1920.0, -758.0, 0.0)

                    if (customblackbox.active)
                    {
                        __instance.transform.localPosition = new Vector3((1920f - buffer) + __instance.pos.x, -((436- bufferh) + __instance.pos.y), 0);
                    }
                    else
                    {
                        __instance.transform.localPosition = new Vector3((1920f * scaledifference.x) + __instance.pos.x, -(436 + __instance.pos.y) * scaledifference.y, 0);
                    }

                }
            }
        }


        //event pictures
        //ResizeScaling

        [HarmonyPatch(typeof(evtPicture), "ResizeScaling")]
        public static class EvtPictureScale
        {
            public static void Postfix()
            {
                if (useresolutionoverride && evtPicture.pictures != null)
                {
                    evtPicture.pictures.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    evtPicture.pictures.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    evtPicture.pictures.GetComponent<CanvasScaler>().matchWidthOrHeight = 1;
                    evtPicture.pictures.GetComponent<CanvasScaler>().scaleFactor = ResolutionOverrideY.Value / 1080f;
                }
            }
        }



        //keeps track of post processing object for easy bloom enable/disable
        [HarmonyPatch(typeof(PostProcessStackV2Manager), "Start")]
        public static class postprocesstrack
        {
            public static void Postfix(ref PostProcessStackV2Manager __instance)
            {
                ppsv2 = __instance;
            }
        }



        /// <summary>
        /// These are the patches for resolution override and shadow override.
        /// </summary>

        public static int fix720p = 5;

        //enforces rendering resolution
        //not sure where aspect ratio enforcing code is
        [HarmonyPatch(typeof(dds3ConfigGraphicsSteam), "GetRenderingScale")]
        public static class resolutionset1
        {
            public static void Postfix(ref int w, ref int h)
            {
                if (useresolutionoverride)
                {
                    w = ResolutionOverrideX.Value;
                    h = ResolutionOverrideY.Value;

                    //this fixes some bizarre bugs where scale doesnt apply initially, or even just does a complete white screen at specifically 720p
                    //dont ask questions.. this just has to happen

                    if (fix720p > 0)
                    {
                        fix720p--;
                        w = 1920;
                        h = 1080;
                    }
                }
            }
        }



        //I dont really get what this is for, it just seems like it renders the regular game texture onto this to once again scale it, for some reason
        [HarmonyPatch(typeof(dds3DefaultMain), "CreateRenderScaleTexture")]
        public static class resolutionset2
        {
            public static void Prefix(ref int w, ref int h)
            {
                //dont do anything if user has set it to 0,0
                if (useresolutionoverride)
                {
                    w = ResolutionOverrideX.Value;
                    h = ResolutionOverrideY.Value;
                }
            }
        }



        //status menu demon view resolution
        //the status texture is infact just loaded right here and never stored to a variable, at least not one I can find easily
        [HarmonyPatch(typeof(cmpStatus), "cmpSetupObjUI")]
        public static class resolutionset3
        {
            public static void Postfix()
            {

                if (useresolutionoverride)
                {
                    RenderTexture status = GlobalData.kernelObject.campObjCamera.GetComponent<Camera>().activeTexture;

                    int w = ResolutionOverrideX.Value;
                    int h = ResolutionOverrideY.Value;

                    if (status.width != w || status.height != h)
                    {
                        status.Release();
                        status.width = (int)(h * (1920f/1080f));
                        status.height = h;
                        status.Create();
                    }
                }
                
            }
        }



        //patches shadow quality
        //default size is 512x512
        [HarmonyPatch(typeof(dds3DefaultMain), "GetShadowMapTex")]
        public static class shadowset
        {
            public static void Postfix(ref RenderTexture __result)
            {

                int w = ShadowOverrideX.Value;
                int h = ShadowOverrideY.Value;

                //dont do anything if user has set it to 0,0
                if (w == 0 && h == 0)
                {
                    return;
                }

                if (__result.width != w || __result.height != h)
                {
                    __result.Release();
                    __result.width = w;
                    __result.height = h;
                    __result.Create();
                }
            }
        }



        //This patches multiple render textures, and anything else that needs fixing within the screen rendering loop

        public static bool refreshblit = true;
        public static bool nowmodetemp = false;

        public static float distortionblurspeedtemp = 0f;

        [HarmonyPatch(typeof(PostBlur), "OnRenderImage")]
        public static class renderfix1
        {
            public static void Prefix(ref PostBlur __instance)
            {
                if (DetermineLate30fps())
                {
                    distortionblurspeedtemp = dds3KernelDraw.Postblur_distortionblur_speed;

                    nowmodetemp = dds3KernelDraw.Postblur_AccumTex_NowMode;
                }

                if (Unlock)
                {
                    //fixes speed of the weird fog noise effect in situations like intro teacher cutscene
                    __instance.distortionblur_moyarotspd = Mathf.Min(__instance.distortionblur_moyarotspd * 30f * RealDeltaTime, __instance.distortionblur_moyarotspd);

                    //fixes the distortion effect on ghosts and such
                    //uses a fixed estimate of deltatime because if this value varies in any way constantly then it jitters super fast because of how the shader works
                    dds3KernelDraw.Postblur_distortionblur_speed = Mathf.Min(distortionblurspeedtemp * 30f * (1f / CurrentFramerate), dds3KernelDraw.Postblur_distortionblur_speed);
                }

                if (Unlock && !speedhack && nowmodetemp)
                {
                    __instance.refresh_Blit = refreshblit;
                }


                //override accumtexture res and stuff
                if (useresolutionoverride)
                {
                    int w = ResolutionOverrideX.Value;
                    int h = ResolutionOverrideY.Value;

                    GlobalData.screen_wid = w;
                    GlobalData.screen_hei = h;

                    //using scale texture to determine when reapplication needs to happen
                    //usually this is only recalled if the user toggles and applies their graphics settings
                    //this also allows you to change override at runtime easily with say the unity UE console, like so;

                    //Nocturne_Graphics_Configurator.NocturneGraphicsConfigurator.ResolutionOverrideX.Value = 3840;
                    //Nocturne_Graphics_Configurator.NocturneGraphicsConfigurator.ResolutionOverrideY.Value = 2160;

                    RenderTexture sclchg = GlobalData.kernelObject.scaleChangeTexture;
                    
                    if ((sclchg.width != w && sclchg.height != h))
                    {
                        ForceRefreshGraphics();
                    }
                }
            }

            //never confuse the game about the screen size outside of rendering code, because it breaks placement and stuff
            //1080p is the default value of this, even at other resolutions I believe, I dont know why this is an actual property
            public static void Postfix()
            {
                if (useresolutionoverride)
                {
                    GlobalData.screen_wid = 1920;
                    GlobalData.screen_hei = 1080;
                }
            }
        }


        public static void ForceRefreshGraphics()
        {
            dds3ConfigGraphicsSteam.ChgConfigGraphicsAll();

            var b = GlobalData.kernelObject.MainCamera.GetComponent<PostBlur>();

            if (b != null)
            {
                GameObject.Destroy(b.accumTexture);
                b.accumTexture = null;
            }
        }


        //patches UI elements as well as general screen elements to fit any aspect ratio given

        [HarmonyPatch(typeof(SteamCameraControl), "Update")]
        public static class elementsRatioPatch
        {
            public static void Prefix(ref SteamCameraControl __instance)
            {
                if (useresolutionoverride)
                {
                    GlobalData.screen_wid = ResolutionOverrideX.Value;
                    GlobalData.screen_hei = ResolutionOverrideY.Value;
                }
            }
            public static void Postfix(ref SteamCameraControl __instance)
            {
                if (useresolutionoverride)
                {
                    GlobalData.screen_wid = 1920;
                    GlobalData.screen_hei = 1080;
                }
            }
        }



        [HarmonyPatch(typeof(UICameraResize), "Update")]
        public static class elementsRatioPatch2
        {
            public static void Prefix(ref UICameraResize __instance)
            {
                if (useresolutionoverride)
                {
                    GlobalData.screen_wid = ResolutionOverrideX.Value;
                    GlobalData.screen_hei = ResolutionOverrideY.Value;
                }
            }
            public static void Postfix(ref UICameraResize __instance)
            {
                if (useresolutionoverride)
                {
                    GlobalData.screen_wid = 1920;
                    GlobalData.screen_hei = 1080;

                    __instance.cam.aspect = aspectratio;
                    __instance.cam.rect = new Rect(0, 0, ResolutionOverrideX.Value, ResolutionOverrideY.Value);
                    __instance.cam.ResetAspect();
                }
            }
        }







        //two little patches that help the above patch, not sure if it needed to be this complicated but it works so..

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


    }
}


