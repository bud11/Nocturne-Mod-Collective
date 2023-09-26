using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime;

[assembly: MelonInfo(typeof(DemiConsolidator.DemiConsolidator), "DemiConsolidator", "1.0", "vvdashdash")]
[assembly: MelonGame(null, "smt3hd")]


namespace DemiConsolidator
{

    public class DemiConsolidator : MelonMod
    {

        private const bool USEDEMONREPLACE = false;   //set this to build for demon or nodemon versions

        private const bool FORCEJACKET = false;      //set this to use the jacket forms from save instead of main



        //I would annotate this code but its super messy and unorganized, it really just sucks, so if you have any specific questions ask me on discord
        

        static GameObject? FLDPLAYERCLONE = null;

        static GameObject? CURRENTPLAYERMODELLATCH = null;
        static GameObject? materialreferenceObject;
        static GameObject? PASKINClone = null;

        static MaterialPropertyBlock? TattooReferenceBlock;

        private Transform? TargetBoneRoot;
        private Transform? NewBoneRoot;

        private GameObject? CharacterRoot;



        private CharacterCloneMode clonemode;
        enum CharacterCloneMode
        {
            Cutscene,  //cutscene bone map
            Battle,    //battle demon
            Amala,     //amala minigame
            Player,    //onto regular fldplayer
        }


        private Dictionary<Transform, Transform> BoneMap = new();
        private static bool needsbonerefresh = false;         //can force to constant true if you want debug realtime updates with hot reload


        enum DisplayChoices
        {
            Normal,
            Hoodie,
            Leather
        }

        DisplayChoices LASTCHOICE = DisplayChoices.Normal;


        private void HideTeethEyes(Transform root)
        {
            //also hide teeth and eyes
            RecursiveFindChild(root.transform, "0x3d_Upper_Tooth").gameObject.active = false;
            RecursiveFindChild(root.transform, "0x3d_Upper_Tooth01").gameObject.active = false;

            RecursiveFindChild(root.transform, "eye_l").gameObject.active = false;
            RecursiveFindChild(root.transform, "eye_l01").gameObject.active = false;
        }

        private void SetMatPropBlock()
        {
            TattooReferenceBlock = new();
            materialreferenceObject.GetComponent<SkinnedMeshRenderer>().GetPropertyBlock(TattooReferenceBlock);
        }


        private static int lastamountofcharacterchildren = 0;

        public override void OnLateUpdate()
        {

            if (GlobalData.kernelObject != null && GlobalData.kernelObject.initflag && dds3GlobalWork.DDS3_GBWK != null)
            {

                var choice = DisplayChoices.Normal;

                if (FORCEJACKET)
                {
                    bool leather = EventBit.evtBitCheck(2224);

                    choice = leather ? DisplayChoices.Leather : DisplayChoices.Hoodie;
                }

                //choice = DisplayChoices.Hoodie;

                if (CharacterRoot == null)
                {
                    CharacterRoot = GameObject.Find("Character");
                }

                //if (CURRENTPLAYERMODELLATCH == null && FLDPLAYERCLONE != null) GameObject.Destroy(FLDPLAYERCLONE);


                if (FLDPLAYERCLONE == null || LASTCHOICE != choice)
                {
                    if (FLDPLAYERCLONE != null) GameObject.Destroy(FLDPLAYERCLONE);

                    AssetBundle bundle = GlobalData.asset_bc.GetAB("fldplayer");

                    needsbonerefresh = true;


                    string assetname = "player_tatoo";
                    switch (choice)
                    {
                        case DisplayChoices.Hoodie:
                            assetname = "player_first2";
                            break;

                        case DisplayChoices.Leather:
                            assetname = "player_first";
                            break;
                    }


                    if (bundle != null)
                    {
                        var loaded = bundle.LoadAsset(assetname, Il2CppType.Of<GameObject>());

                        //var loaded = GlobalData.asset_bc.LoadPrefab("fldplayer", Il2Cppxrd773.Define.FilePathConstans.FieldCharactorBFullPath, assetname);

                        
                        if (loaded != null)
                        {
                            FLDPLAYERCLONE = GameObject.Instantiate(loaded).Cast<GameObject>();
                            FLDPLAYERCLONE.name = "PLAYER_CLONE_FOR_CONSOLIDATE_MOD";

                            var t = FLDPLAYERCLONE.transform.GetChild(0);
                            if (t.childCount == 1) PASKINClone = FLDPLAYERCLONE.transform.GetChild(1).gameObject;
                            else PASKINClone = t.GetChild(1).gameObject;


                            for (int child = 0; child < PASKINClone.transform.parent.childCount; child++)
                            {
                                var CHILD = PASKINClone.transform.parent.GetChild(child).gameObject;

                                if (CHILD.name.Contains("target") || CHILD.name.Contains("tel")) CHILD.active = false;
                            }



                            var comp = PASKINClone.GetComponent<SkinnedMeshRenderer>();
                            List<Material> mats = new();
                            foreach (Material mat in comp.materials)
                            {
                                mats.Add(GameObject.Instantiate(mat));
                            }
                            comp.materials = mats.ToArray();


                            LASTCHOICE = choice;
                        }
                    }

                    
                }

                else
                {


                    if (CURRENTPLAYERMODELLATCH != null && !CURRENTPLAYERMODELLATCH.transform.gameObject.activeInHierarchy)
                    {
                        CURRENTPLAYERMODELLATCH = null;
                    }

                    if (CURRENTPLAYERMODELLATCH == null)
                    {
                        TattooReferenceBlock = null;
                    }




                    if (CharacterRoot != null && (CURRENTPLAYERMODELLATCH == null || (lastamountofcharacterchildren != CharacterRoot.transform.childCount) || !CURRENTPLAYERMODELLATCH.activeInHierarchy))
                    {
                        lastamountofcharacterchildren = CharacterRoot.transform.childCount;


                        var old = CURRENTPLAYERMODELLATCH;

                        for (int i = 0; i < CharacterRoot.transform.childCount; i++)
                        {

                            GameObject Child = CharacterRoot.transform.GetChild(i).gameObject;


                            //MelonLogger.Msg(root.transform.GetChild(i).name);
                            if (Child.name.Contains("player_tatoo_e"))
                            {
                                CURRENTPLAYERMODELLATCH = Child;
                                clonemode = CharacterCloneMode.Cutscene;
                                //needsbonerefresh = true;

                                TargetBoneRoot = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(0);

                                materialreferenceObject = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(1).gameObject;


                                SetMatPropBlock();

                                //break;
                            }


                            else if (Child.name.Contains("devil_0x00"))
                            {
                                if (USEDEMONREPLACE)
                                {
                                    CURRENTPLAYERMODELLATCH = Child;
                                    clonemode = CharacterCloneMode.Battle;
                                    //needsbonerefresh = true;

                                    TargetBoneRoot = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(0);

                                    materialreferenceObject = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(1).gameObject;

                                    HideTeethEyes(CURRENTPLAYERMODELLATCH.transform);

                                    SetMatPropBlock();
                                }

                                //break;
                            }


                            //for status menu
                            else if (USEDEMONREPLACE && Child.name.Contains("boot_0x00"))
                            {
                                if (Child.transform.FindChild("devil_0x00") != null)
                                {

                                    CURRENTPLAYERMODELLATCH = Child.transform.FindChild("devil_0x00").gameObject;
                                    CURRENTPLAYERMODELLATCH.transform.eulerAngles = new Vector3(0, 180, 0);

                                    clonemode = CharacterCloneMode.Battle;
                                    //needsbonerefresh = true;

                                    TargetBoneRoot = CURRENTPLAYERMODELLATCH.transform.GetChild(0);

                                    materialreferenceObject = CURRENTPLAYERMODELLATCH.transform.GetChild(1).gameObject;

                                    HideTeethEyes(CURRENTPLAYERMODELLATCH.transform);


                                    SetMatPropBlock();

                                    //break;
                                }
                            }


                            //amala
                            else if (Child.name == "Player")
                            {
                                CURRENTPLAYERMODELLATCH = Child;

                                TargetBoneRoot = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(3);

                                materialreferenceObject = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(0).gameObject;

                                clonemode = CharacterCloneMode.Amala;
                                //needsbonerefresh = true;

                                SetMatPropBlock();


                                //break;
                            }


                            //normal player in field, here for jacket support 

                            else if (Child.name.Contains("PLAYER_UNIT"))
                            {

                                if (Child.transform.FindChild("player_tatoo") != null) //dont replace actually intended jacket forms, thats a whole other can of worms
                                {
                                    CURRENTPLAYERMODELLATCH = Child;
                                    clonemode = CharacterCloneMode.Player;
                                    //needsbonerefresh = true;

                                    TargetBoneRoot = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(0);

                                    materialreferenceObject = CURRENTPLAYERMODELLATCH.transform.GetChild(0).GetChild(1).gameObject;

                                    SetMatPropBlock();

                                }
                            }

                        }

                        if (old != CURRENTPLAYERMODELLATCH) needsbonerefresh = true;

                    }



                    if (CURRENTPLAYERMODELLATCH != null && CURRENTPLAYERMODELLATCH.activeSelf)
                    {
                        FLDPLAYERCLONE.transform.position = CURRENTPLAYERMODELLATCH.transform.position;


                        //deactivate player clone animator
                        var comp = FLDPLAYERCLONE.transform.GetChild(0).GetComponent<Animator>();
                        if (comp == null) comp = FLDPLAYERCLONE.transform.GetComponent<Animator>();

                        if (comp != null) comp.enabled = false;


                        NewBoneRoot = FLDPLAYERCLONE.transform.GetChild(0);

                        FLDPLAYERCLONE.layer = CURRENTPLAYERMODELLATCH.layer;

                        PASKINClone.layer = CURRENTPLAYERMODELLATCH.layer;
                        FLDPLAYERCLONE.transform.SetParent(CURRENTPLAYERMODELLATCH.transform.parent.parent);


                        materialreferenceObject.GetComponent<SkinnedMeshRenderer>().sharedMesh = null;



                        //MelonLogger.Msg(clonemode);

                        FieldChrHD Original = CURRENTPLAYERMODELLATCH.transform.GetParent().GetComponent<FieldChrHD>();
                        //FieldChrHD New = FLDPLAYERCLONE.GetComponent<FieldChrHD>();

                        if (Original == null)
                        {
                            Original = CURRENTPLAYERMODELLATCH.GetComponent<FieldChrHD>();
                        }
                        

                        if (Original != null)
                        {
                            var copy = new Il2CppSystem.Collections.Generic.List<Material>();
                            foreach (var m in Original.mat_list)
                            {
                                copy.Add(m);
                            }

                            foreach (Material mat in PASKINClone.GetComponent<SkinnedMeshRenderer>().materials)
                            {
                                Original.mat_list.Add(mat);

                                //tattoo colors, controlled by animation
                                if (mat.HasProperty("_Ramp1Color")) mat.SetColor("_Ramp1Color", TattooReferenceBlock.GetColor("_Ramp1Color"));
                                if (mat.HasProperty("_Ramp1BlendPower")) mat.SetFloat("_Ramp1BlendPower", TattooReferenceBlock.GetFloat("_Ramp1BlendPower"));
                            }


                            Original.SetLightAll(Original.gameObject);
                            Original.SetLayerAll(Original.gameObject);
                            

                            foreach (Material mat in PASKINClone.GetComponent<SkinnedMeshRenderer>().materials)
                            {
                                //if (mat.HasProperty("_ToonType")) mat.SetFloat("_ToonType", Mathf.Max(mat.GetFloat("_ToonType"), 3f));
                                if (mat.HasProperty("_Toon")) mat.SetTexture("_Toon", copy[0].GetTexture("_Toon"));
                            }

                            Original.mat_list = copy;
                        }



                        if (needsbonerefresh)
                        {
                            BoneMap.Clear();
                            needsbonerefresh = false;


                            switch (clonemode)
                            {
                                case CharacterCloneMode.Cutscene:
                                    CutsceneSet();
                                    break;

                                case CharacterCloneMode.Battle:
                                    DevilSet();
                                    break;

                                case CharacterCloneMode.Amala:
                                    CutsceneSet();
                                    break;

                                case CharacterCloneMode.Player:
                                    PlayerSet();
                                    break;
                            }


                            if (choice == DisplayChoices.Normal && clonemode == CharacterCloneMode.Player)
                            {
                                PASKINClone.GetComponent<SkinnedMeshRenderer>().materials = materialreferenceObject.GetComponent<SkinnedMeshRenderer>().materials;
                            }

                        }

                        else
                        {

                            foreach (KeyValuePair<Transform,Transform> pair in BoneMap)
                            {
                                pair.Key.position = pair.Value.position;
                                pair.Key.rotation = pair.Value.rotation;
                            }
                        }

                        var reflect = GameObject.Find("player_tatoo(Clone)");
                        if (reflect != null)
                        {
                            reflect.active = false;
                        }


                        FLDPLAYERCLONE.active = true;

                    }
                    else
                    {
                        FLDPLAYERCLONE.active = false;
                    }
                }

                if (FLDPLAYERCLONE != null && FLDPLAYERCLONE.transform.GetParent() == null && CharacterRoot != null)
                {
                    FLDPLAYERCLONE.transform.SetParent(CharacterRoot.transform);
                }
                base.OnLateUpdate();
            }
        }



        private void DevilSet()
        {

            //SetBoneTransformToMatch("Bip01_Pelvis_dammy", "p_a_Pelvis_real");
            //SetBoneTransformToMatch("p_a_Spine", "p_a_Spine_real");


            SetBoneTransformToMatch("Bip01_Pelvis", "p_a_Pelvis");

            //upper body
            SetBoneTransformToMatch("Bip01_Spine", "p_a_Spine");
            SetBoneTransformToMatch("Bip01_Spine1", "p_a_Spine1");
            SetBoneTransformToMatch("Bip01_Spine2", "p_a_Spine2");
            //SetBoneTransformToMatch("Bip01_neck_dammy", "p_a_Neck_real");



            SetBoneTransformToMatch("Bip01_L_Thigh", "p_a_L_Thigh");
            SetBoneTransformToMatch("Bip01_R_Thigh", "p_a_R_Thigh");

            //left leg
            SetBoneTransformToMatch("Bip01_L_Calf", "p_a_L_Calf");
            SetBoneTransformToMatch("Bip01_L_Foot", "p_a_L_Foot");
            SetBoneTransformToMatch("Bip01_L_Toe0", "p_a_L_Toe0");


            //right leg
            SetBoneTransformToMatch("Bip01_R_Calf", "p_a_R_Calf");
            SetBoneTransformToMatch("Bip01_R_Foot", "p_a_R_Foot");
            SetBoneTransformToMatch("Bip01_R_Toe0", "p_a_R_Toe0");



            //neck and clavicle
            SetBoneTransformToMatch("Bip01_L_Clavicle", "p_a_L_Clavicle");

            SetBoneTransformToMatch("Bip01_R_Clavicle", "p_a_R_Clavicle");

            SetBoneTransformToMatch("Bip01_neck", "p_a_Neck");

            //head
            SetBoneTransformToMatch("Bip01_Head", "p_a_Head");





            //left arm
            SetBoneTransformToMatch("Bip01_L_UpperArm", "p_a_L_UpperArm");

            SetBoneTransformToMatch("Bip01_L_Forearm", "p_a_L_Forearm");

            SetBoneTransformToMatch("Bip01_L_Hand", "p_a_L_Hand");


            //FINGER 0
            SetBoneTransformToMatch("Bip01_L_Finger0", "p_a_L_Finger0");

            SetBoneTransformToMatch("Bip01_L_Finger01", "p_a_L_Finger01");




            //FINGER 1
            SetBoneTransformToMatch("Bip01_L_Finger1", "p_a_L_Finger1");

            SetBoneTransformToMatch("Bip01_L_Finger11", "p_a_L_Finger11");




            //FINGER 2
            SetBoneTransformToMatch("Bip01_L_Finger2", "p_a_L_Finger2");

            SetBoneTransformToMatch("Bip01_L_Finger21", "p_a_L_Finger21");




            //Right arm
            SetBoneTransformToMatch("Bip01_R_UpperArm", "p_a_R_UpperArm");

            SetBoneTransformToMatch("Bip01_R_Forearm", "p_a_R_Forearm");

            SetBoneTransformToMatch("Bip01_R_Hand", "p_a_R_Hand");

            //FINGER 0
            SetBoneTransformToMatch("Bip01_R_Finger0", "p_a_R_Finger0");

            SetBoneTransformToMatch("Bip01_R_Finger01", "p_a_R_Finger01");


            //FINGER 1
            SetBoneTransformToMatch("Bip01_R_Finger1", "p_a_R_Finger1");

            SetBoneTransformToMatch("Bip01_R_Finger11", "p_a_R_Finger11");



            //FINGER 2
            SetBoneTransformToMatch("Bip01_R_Finger2", "p_a_R_Finger2");

            SetBoneTransformToMatch("Bip01_R_Finger21", "p_a_R_Finger21");



        }



        private void CutsceneSet()
        {
            //this is as much a pain in the ass as it looks like.
            SetBoneTransformToMatch("p_a", "p_a");



            bool spinemodeFUCKED = RecursiveFindChild(TargetBoneRoot, "p_a Pelvis_本物") == null && !first2;

            if (spinemodeFUCKED)
            {
                //MelonLogger.Msg("FUCKED!!");
                //SetBoneTransformToMatch("p_a Pelvis", "p_a_Pelvis_real");
                //SetBoneTransformToMatch("p_a 脊椎", "p_a_Spine_real");
                SetBoneTransformToMatch("p_a 脊椎1", "p_a_Pelvis");

                SetBoneTransformToMatch("p_a 脊椎2", "p_a_Spine");
                SetBoneTransformToMatch("p_a 脊椎3", "p_a_Spine1");
                SetBoneTransformToMatch("p_a 脊椎4", "p_a_Spine2");
                //SetBoneTransformToMatch("p_a 首", "p_a_Neck_real");


            }
            else
            {
                //spine and neck bones
                //SetBoneTransformToMatch("p_a Pelvis_本物", "p_a_Pelvis_real");
                //SetBoneTransformToMatch("p_a 脊椎_本物", "p_a_Spine_real");
                SetBoneTransformToMatch("p_a Pelvis", "p_a_Pelvis");

                //upper body
                SetBoneTransformToMatch("p_a 脊椎", "p_a_Spine");
                SetBoneTransformToMatch("p_a 脊椎1", "p_a_Spine1");
                SetBoneTransformToMatch("p_a 脊椎2", "p_a_Spine2");
                //SetBoneTransformToMatch("p_a 首_本物", "p_a_Neck_real");
            }



            SetBoneTransformToMatch("p_a L Thigh", "p_a_L_Thigh");
            SetBoneTransformToMatch("p_a R Thigh", "p_a_R_Thigh");

            //left leg
            SetBoneTransformToMatch("p_a L Calf", "p_a_L_Calf");
            SetBoneTransformToMatch("p_a L Foot", "p_a_L_Foot");
            SetBoneTransformToMatch("p_a L Toe0", "p_a_L_Toe0");


            //right leg
            SetBoneTransformToMatch("p_a R Calf", "p_a_R_Calf");
            SetBoneTransformToMatch("p_a R Foot", "p_a_R_Foot");
            SetBoneTransformToMatch("p_a R Toe0", "p_a_R_Toe0");



            //neck and clavicle
            SetBoneTransformToMatch("p_a L Clavicle", "p_a_L_Clavicle");

            SetBoneTransformToMatch("p_a R Clavicle", "p_a_R_Clavicle");

            SetBoneTransformToMatch("p_a 首", "p_a_Neck");




            //real neck and head
            SetBoneTransformToMatch("p_a Head", "p_a_Head");

            SetBoneTransformToMatch("p_a HeadNub", "p_a_HeadNub");

            //lefteye
            SetBoneTransformToMatch("p_a_eyeL_00", "p_a_eyeL_00");

            SetBoneTransformToMatch("p_a_eyeL_01", "p_a_eyeL_01");

            //righteye
            SetBoneTransformToMatch("p_a_eyeR_00", "p_a_eyeR_00");

            SetBoneTransformToMatch("p_a_eyeR_01", "p_a_eyeR_01");





            //left arm
            SetBoneTransformToMatch("p_a L UpperArm", "p_a_L_UpperArm");

            SetBoneTransformToMatch("p_a L Forearm", "p_a_L_Forearm");

            SetBoneTransformToMatch("p_a L Hand", "p_a_L_Hand");


            //FINGER 0
            SetBoneTransformToMatch("p_a L Finger0", "p_a_L_Finger0");

            SetBoneTransformToMatch("p_a L Finger01", "p_a_L_Finger01");




            //FINGER 1
            SetBoneTransformToMatch("p_a L Finger1", "p_a_L_Finger1");

            SetBoneTransformToMatch("p_a L Finger11", "p_a_L_Finger11");



            //FINGER 2
            SetBoneTransformToMatch("p_a L Finger2", "p_a_L_Finger2");

            SetBoneTransformToMatch("p_a L Finger21", "p_a_L_Finger21");




            //Right arm
            SetBoneTransformToMatch("p_a R UpperArm", "p_a_R_UpperArm");

            SetBoneTransformToMatch("p_a R Forearm", "p_a_R_Forearm");

            SetBoneTransformToMatch("p_a R Hand", "p_a_R_Hand");

            //FINGER 0
            SetBoneTransformToMatch("p_a R Finger0", "p_a_R_Finger0");

            SetBoneTransformToMatch("p_a R Finger01", "p_a_R_Finger01");


            //FINGER 1
            SetBoneTransformToMatch("p_a R Finger1", "p_a_R_Finger1");

            SetBoneTransformToMatch("p_a R Finger11", "p_a_R_Finger11");



            //FINGER 2
            SetBoneTransformToMatch("p_a R Finger2", "p_a_R_Finger2");

            SetBoneTransformToMatch("p_a R Finger21", "p_a_R_Finger21");

        }

        private static bool first2 = false;


        private void PlayerSet()
        {
            first2 = true;
            CutsceneSet();
            first2 = false;
        }



        private static Dictionary<string, string> FldtoJacketMap = new()
        {

            { "p_a", "p_b" },
            { "p_a_Pelvis", "p_b Pelvis" },
            { "p_a_Spine", "p_b 脊椎" },
            { "p_a_Spine1", "p_b 脊椎1" },
            { "p_a_Spine2", "p_b 脊椎2" },
            { "p_a_L_Thigh", "p_b L Thigh" },
            { "p_a_R_Thigh", "p_b R Thigh" },
            { "p_a_L_Calf", "p_b L Calf" },
            { "p_a_L_Foot", "p_b L Foot" },
            { "p_a_L_Toe0", "p_b L Toe0" },
            { "p_a_R_Calf", "p_b R Calf" },
            { "p_a_R_Foot", "p_b R Foot" },
            { "p_a_R_Toe0", "p_b R Toe0" },
            { "p_a_L_Clavicle", "p_b L Clavicle" },
            { "p_a_R_Clavicle", "p_b R Clavicle" },
            { "p_a_Neck", "p_b 首" },
            { "p_a_Head", "p_b Head" },
            { "p_a_HeadNub", "p_b HeadNub" },
            { "p_a_eyeL_00", "p_b_eyeL_00" },
            { "p_a_eyeL_01", "p_b_eyeL_01" },
            { "p_a_eyeR_00", "p_b_eyeR_00" },
            { "p_a_eyeR_01", "p_b_eyeR_01" },
            { "p_a_L_UpperArm", "p_b L UpperArm" },
            { "p_a_L_Forearm", "p_b L Forearm" },
            { "p_a_L_Hand", "p_b L Hand" },
            { "p_a_L_Finger0", "p_b L Finger0" },
            { "p_a_L_Finger01", "p_b L Finger01" },
            { "p_a_L_Finger1", "p_b L Finger1" },
            { "p_a_L_Finger11", "p_b L Finger11" },
            { "p_a_L_Finger2", "p_b L Finger2" },
            { "p_a_L_Finger21", "p_b L Finger21" },
            { "p_a_R_UpperArm", "p_b R UpperArm" },
            { "p_a_R_Forearm", "p_b R Forearm" },
            { "p_a_R_Hand", "p_b R Hand" },
            { "p_a_R_Finger0", "p_b R Finger0" },
            { "p_a_R_Finger01", "p_b R Finger01" },
            { "p_a_R_Finger1", "p_b R Finger1" },
            { "p_a_R_Finger11", "p_b R Finger11" },
            { "p_a_R_Finger2", "p_b R Finger2" },
            { "p_a_R_Finger21", "p_b R Finger21" }

        };




        private bool SetBoneTransformToMatch(string oldindexpath, string newindexpath)
        {

            if (LASTCHOICE == DisplayChoices.Normal)
            {
                if (clonemode == CharacterCloneMode.Player) oldindexpath = newindexpath;
            }

            else if (first2)
            {
                oldindexpath = newindexpath;
                newindexpath = FldtoJacketMap[newindexpath];
            }

            else
            {
                newindexpath = FldtoJacketMap[newindexpath];
            }


            Transform targb = RecursiveFindChild(TargetBoneRoot, oldindexpath);

            Transform newb = RecursiveFindChild(NewBoneRoot, newindexpath);

            //MelonLogger.Msg(targb.name + " = " + newb.name);
            //MelonLogger.Msg(newb == null);

            if (newb == null || targb == null)
            {
                MelonLogger.Msg("BONE EQUIVALENT MISSING!! " + oldindexpath + " = " + newindexpath);

                needsbonerefresh = true;
                return false;
            }

            newb.position = targb.position;
            newb.rotation = targb.rotation;

            BoneMap.Add(newb.transform, targb.transform);


            return true;

        }

        Transform RecursiveFindChild(Transform parent, string childName)
        {

            foreach (Il2CppSystem.Object chld in parent)
            {
                Transform child = chld.Cast<Transform>();

                if (child.name == childName)
                {
                    return child;
                }
                else
                {
                    Transform found = RecursiveFindChild(child, childName);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }


    }
}
