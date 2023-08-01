using System;
using System.Collections.Generic;

using MelonLoader;
using static MelonLoader.MelonLogger;

using UnityEngine;
using HarmonyLib;
using UnityEngine.UI;
using System.Reflection;
using Il2Cpp;
using Nocturne_Minimap;


[assembly: MelonInfo(typeof(ModClass), "Minimap mod", "1.44", "vv--")]
[assembly: MelonGame(null, "smt3hd")]

namespace Nocturne_Minimap
{
    public class ModClass : MelonMod
    {

        public static bool MODDEBUGMODE = false;

        public static GameObject originalMap = null;
        public static GameObject clonemap = null;
        public static GameObject fcompass = null;
        public static GameObject custombase = null;

        public static GameObject viewclone = null;
        public static GameObject selfclone = null;
        public static GameObject viewog = null;
        public static GameObject selfog = null;



        public static void CreateSetup()
        {
            //Msg("started creation");
            //clean up
            if (custombase != null)
            {
                GameObject.DestroyImmediate(custombase);
            }


            //get original map and create copy for corner
            originalMap = GlobalData.kernelObject.autoMapObj;

            clonemap = GameObject.Instantiate(originalMap);
            clonemap.name = "automapUI_vvcopy";

            clonemap.GetComponent<UiHD>().enabled = false;
            clonemap.GetComponent<autoMapUI>().enabled = false;


            HashSet<GameObject> destroy = new HashSet<GameObject>();
            for (int i = 0; i < clonemap.transform.childCount; i++)
            {
                if (clonemap.transform.GetChild(i).gameObject.name.Contains("autm_frame"))
                {
                    destroy.Add(clonemap.transform.GetChild(i).gameObject);
                }
            }
            foreach (GameObject x in destroy)
            {
                GameObject.DestroyImmediate(x);
            }

            //turns out this only works properly if you're playing in english
            //GameObject.Destroy(clonemap.transform.Find("autm_frame_oubei").gameObject);
            

            //get compass
            fcompass = GlobalData.kernelObject.enemyUI;

            GameObject fcompassbase = fcompass.transform.FindChild("fcompass_base").gameObject;


            //start setting up, creating and manipulating objects

            custombase = GameObject.Instantiate(fcompass.transform.FindChild("fcompass_base").gameObject);
            custombase.transform.SetParent(fcompassbase.transform);
            custombase.name = "custombase";
            custombase.transform.localScale = Vector3.one * 0.5f;
            custombase.transform.localPosition = Vector3.zero;
            custombase.transform.localRotation = Quaternion.Euler(0, 0, -45);

            custombase.GetComponent<Image>().sprite = new Sprite();

            custombase.AddComponent<Mask>();
            custombase.AddComponent<CanvasGroup>();
            custombase.GetComponent<CanvasGroup>().alpha = 0.7f;

            clonemap.transform.SetParent(custombase.transform);

            clonemap.transform.localScale = Vector3.one * 0.55f;
            clonemap.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            clonemap.transform.localPosition = Vector3.zero;

            fcompassbase.transform.localRotation = Quaternion.Euler(0, 0, 45);

            fcompassbase.GetComponent<Image>().type = Image.Type.Sliced;


            fcompass.transform.localScale = Vector3.one * 1.8f;


            GameObject fcompasstop = fcompass.transform.Find("fcompass_low").gameObject;
            fcompasstop.transform.SetSiblingIndex(fcompass.transform.childCount - 1);

            fcompasstop.transform.rotation = Quaternion.Euler(0, 0, 45);

            GameObject fcompasshigh = fcompass.transform.Find("fcompass_high").gameObject;
            fcompasshigh.transform.SetSiblingIndex(fcompass.transform.childCount - 1);

            fcompasshigh.transform.rotation = Quaternion.Euler(0, 0, 45);

            GameObject stat = fcompass.transform.Find("fcompass_status").gameObject;
            stat.transform.localScale = Vector3.one * 0.5f;
            stat.transform.localPosition = new Vector3(72, -72, 0);


            GameObject dungeon = fcompass.transform.Find("dungeon").gameObject;


            //prepare the compass direction icons

            dungeon.transform.Find("fcompass_selfveiw").gameObject.SetActive(false);

            GameObject n = dungeon.transform.Find("fcompass_n").gameObject;
            GameObject s = dungeon.transform.Find("fcompass_s").gameObject;
            GameObject e = dungeon.transform.Find("fcompass_e").gameObject;
            GameObject w = dungeon.transform.Find("fcompass_w").gameObject;

            float psize = 0.62f;
            n.transform.localScale = Vector3.one * psize;
            s.transform.localScale = Vector3.one * psize;
            e.transform.localScale = Vector3.one * psize;
            w.transform.localScale = Vector3.one * psize;

            n.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            s.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            e.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            w.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

            int size = 72;
            n.transform.localPosition = new Vector3(0, size, -4);
            s.transform.localPosition = new Vector3(0, -size, -4);
            e.transform.localPosition = new Vector3(size, 0, -4);
            w.transform.localPosition = new Vector3(-size, 0, -4);



            //replace swirl shader with another that supports stencil mask

            GameObject swirl = clonemap.transform.Find("autm_bg/autmbg_vortex").gameObject;
            Texture tex = swirl.GetComponent<Image>().mainTexture;

            swirl.GetComponent<Image>().material = GameObject.Instantiate(clonemap.transform.Find("autm_bg/autmbg_base").GetComponent<Image>().material);

            swirl.GetComponent<Image>().material.SetTexture("_MainTex", tex);
            swirl.GetComponent<Image>().material.SetFloat("_Config_Bright", 1.3f);
            //swirl.GetComponent<Image>().material.SetColor("Tint", new Color32(246, 198, 169, 255));

            clonemap.transform.FindChild("autmmark_selfveiw").transform.localScale = Vector3.one * 1.3f;

            clonemap.transform.localRotation = Quaternion.Euler(0, 0, 0);


            //find player icons

            viewclone = clonemap.transform.Find("autmmark_selfveiw/view").gameObject;
            selfclone = clonemap.transform.Find("autmmark_selfveiw/self").gameObject;

            viewog = originalMap.transform.Find("autmmark_selfveiw/view").gameObject;
            selfog = originalMap.transform.Find("autmmark_selfveiw/self").gameObject;

            //not specifically sure why I have to do this but I do

            viewclone.SetActive(true);
            selfclone.SetActive(true);

        }


        public static bool CanOpenMap()
        {
            //ripped checks straight from automapproc ghidra

            return (9 < fldGlobal.fldGb.fieldID && fldGlobal.fldGb.fieldID < 200);

        }


        public static int lastfloor = 0;


        [HarmonyPatch(typeof(fldProcess), "ProcAmCheck")]
        public static class defaultloop
        {
            public static void Postfix()
            {

                //if setup definitely exists
                if (originalMap != null && fcompass != null && clonemap != null)
                {
                    
                    //if the main vanilla map isnt open, force it to draw current floor at current position, so that minimap can reflect it
                    //AND if the map is actually capable of being opened at this moment
                    if (fldAutoMap.AutoMapSeq == 0 && CanOpenMap())
                    {
                        custombase.active = true;

                        //refresh map icons if floor was changed
                        if (lastfloor != fldGlobal.fldGb.AmFloor || lastfloor != fldAutoMap.gAmap_NowFloor)
                        {
                            ReconstructMap();
                            return;
                        }

                        //based on original code, sets map floor and cam position to where the player is at all times

                        fldAutoMap.gAmap_NowFloor = fldGlobal.fldGb.AmFloor;
                        fldAutoMap.gAmap_InFloor = fldGlobal.fldGb.AmFloor;

                        float x = 0;
                        float z = 0;

                        fldAutoMap.fldAutoMapGetAreaPos(fldGlobal.fldGb.areaID, ref x, ref z);

                        //turns out stutter is because of this weird integer requirement
                        //I do have a good idea of how I could solve this without modifying the method in any way, but I dont have all the information I need to implement it
                        //basically, I'd offset the actual map root object by the small float difference between the real float position and the rounded integer position
                        //but I dont know how to convert nocturne coodinates into unity 2D coodinates, I dunno what the multiplier and/or offset is

                        fldAutoMap.gAmap_OfsX = (int)fldGlobal.fldGb.playerX + (int)x;
                        fldAutoMap.gAmap_OfsZ = (int)fldGlobal.fldGb.playerZ + (int)z;

                        
                        //preserve internal map seq mode if it were for some reason changed
                        int whatever = fldAutoMap.AutoMapSeq;

                        //force map update

                        StopProcesses = true;
                        
                        fldAutoMap.fldAutoMapDrawOneArea();

                        StopProcesses = false;

                        fldAutoMap.AutoMapSeq = whatever;

                        

                    }
                    else
                    {
                        //hide custom base if map cant render
                        custombase.active = false;
                    }

                    //keep player icons in correct positions

                    viewclone.transform.localPosition = viewog.transform.localPosition;
                    selfclone.transform.localPosition = selfog.transform.localPosition;

                    viewclone.transform.localRotation = viewog.transform.localRotation;
                    selfclone.transform.localRotation = selfog.transform.localRotation;


                    //keep icons in correct positions

                    HashSet<string> found = new HashSet<string>();

                    foreach (Il2CppSystem.Object x in originalMap.transform.Find("autom_icon"))
                    {
                        GameObject xgo = x.Cast<Transform>().gameObject;

                        string instanceid = xgo.GetInstanceID().ToString();
                        
                        Transform find = clonemap.transform.Find("autom_icon/" + instanceid);
                        if (find != null)
                        {
                            find.localPosition = xgo.transform.localPosition;
                            find.localRotation = xgo.transform.localRotation;

                            find.gameObject.active = xgo.activeSelf;
                        }

                        else
                        {
                            GameObject copy = GameObject.Instantiate(xgo.gameObject);
                            copy.name = instanceid;
                            copy.transform.SetParent(clonemap.transform.Find("autom_icon"));
                            copy.transform.localPosition = xgo.transform.localPosition;
                            copy.transform.localRotation = xgo.transform.localRotation;

                            copy.gameObject.active = xgo.gameObject.activeSelf;

                            find = copy.transform;
                            find.localScale = Vector3.one;
                        }
                        found.Add(instanceid);
                    }

                    //destroy outdated icons

                    foreach (Il2CppSystem.Object b in clonemap.transform.Find("autom_icon"))
                    {
                        Transform g = b.Cast<Transform>();
                        if (!found.Contains(g.name))
                        {
                            //Msg("destroyed " + g.name);
                            GameObject.DestroyImmediate(g.gameObject);
                        }
                    }
                    found.Clear();
                }


                //not sure if nessecary, probably not, but oh well
                StopProcesses = false;
            }
        }


        //override the position of the compass so it isnt too far into the corner
        [HarmonyPatch(typeof(dds3DefaultMain), "SetEnemyUI")]
        public static class minipatch
        {
            public static void Postfix()
            {
                if (fcompass != null)
                {
                    float offset = 55;
                    //now takes into account screen size
                    fcompass.transform.position += new Vector3(-offset * (Screen.width / 1920f), offset * (Screen.height / 1080f), 0);

                    //Msg((Screen.width / 1920f));
                }
            }
        }





        [HarmonyPatch(typeof(fldAutoMap), "fldAutoMapFree")]
        public static class endpatch
        {
            public static void Prefix(ref bool __runOriginal)
            {
                if (GlobalData.kernelObject.autoMapObj != null)
                {
                    GlobalData.kernelObject.autoMapObj.SetActive(false);
                }
                __runOriginal = false;
            }
        }



        public static bool StopProcesses = false;



        //force map objects to load/be created when any area loads
        //this needs to be logic that gets called ONE TIME when a new area is loaded.

        [HarmonyPatch(typeof(fldTitle), "fldTitleMiniStart2")]
        public static class Loaderpatch
        {
            public static void Postfix()
            {
                ReconstructMap();
            }
        }



        public static void ReconstructMap()
        {
            //Msg("reconstructed map");
            StopProcesses = true;

            if (CanOpenMap())
            {
                fldAutoMap.fldAutoMapSeqStart();

                CreateSetup();

                fldAutoMap.fldAutoMapSeqEnd();

                //dont cancel out input in this scenario
                fldGlobal.fldGb.NoInpAmCnt = 0;
                fldGlobal.fldGb.NoInpPlCnt = 0;

                lastfloor = fldGlobal.fldGb.AmFloor;
            }

            StopProcesses = false;
        }



        //stops the minimap opening sound from being played when Im the one who called the start of the sequence
        // + now stops some other methods in fldprocess to avoid ruining game processes

        [HarmonyReversePatch]
        public static class MuteProcesses
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(dds3PadManager), "DDS3_PADCHECK_PRESS");
                yield return AccessTools.Method(typeof(dds3PadManager), "DDS3_PADCHECK_TRIG");

                yield return AccessTools.Method(typeof(sdfSound), "p2sdPlaySE");

                yield return AccessTools.Method(typeof(fldProcess), "fldProcAmChk");
                yield return AccessTools.Method(typeof(fldProcess), "fldProcOnFlag");
                yield return AccessTools.Method(typeof(fldProcess), "fldProcOffFlag");

                yield return AccessTools.Method(typeof(fldProcess), "fldSeqOffFlag");
                yield return AccessTools.Method(typeof(fldProcess), "fldSeqOnFlag");
                yield return AccessTools.Method(typeof(fldProcess), "fldSeqCheckFlag");

            }

            public static void Prefix(ref bool __runOriginal)
            {
                __runOriginal = !StopProcesses;
            }
        }




    }
}
