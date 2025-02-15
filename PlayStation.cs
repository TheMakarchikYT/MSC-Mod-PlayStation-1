using System;
using System.Diagnostics;
using System.IO;
using HutongGames.PlayMaker;
using ModsShop;
using MSCLoader;
using MSCLoader.Helper;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlayStation
{
    public class PlayStation : Mod
    {
        public override string ID => "PlayStation"; // Your (unique) mod ID 
        public override string Name => "PlayStation 1"; // Your mod name
        public override string Author => "TheMakarchikYT"; // Name of the Author (your name)
        public override string Version => "1.0.2"; // Version
        public override string Description => "Adds a PlayStation 1 into My Summer Car with some extra controllers and memory cards."; // Short description of your mod

        GameObject playStation;
        GameObject playstationController1;
        GameObject memoryCard1;

        private GameObject _prefabPlayStation;
        private GameObject _prefabPlayStationController;
        private GameObject _prefabMemoryCard;
        private GameObject _prefabMemoryCardDisplay;
        private GameObject _prefabControllerDisplay;

        private GameObject _box;
        private AudioSource _unpackSound;


        // RetroArch variables
        private Process retroArchProcess;
        private string retroArchPath;
        private string retroArchCorePath;
        private string biosPath;
        private string romsPath;
        private string biosbinPath;
        private GameObject tvScreen;
        private RenderTexture renderTexture;
        private Material tvScreenMaterial;


        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.Update, Mod_Update);
            SetupFunction(Setup.OnSave, Mod_Save);
            SetupFunction(Setup.OnGUI, Mod_OnGUI);
        }

        private bool hasPurchased;
        private bool hasUnpacked;

        private readonly GameObject[] _memoryCards = new GameObject[10]; // Additional memory cards
        private readonly GameObject[] _controllers = new GameObject[5]; // Additional controllers

        private void Mod_Save()
        {
            SaveLoad.WriteValue(this, "PlayStationPurchased", hasPurchased);
            if (!hasPurchased) return;
            SaveLoad.WriteValue(this, "PlayStationUnpacked", hasUnpacked);
            if (hasUnpacked)
            {
                SaveLoad.WriteValue(this, "PlaystationPosition", playStation.transform.position);
                SaveLoad.WriteValue(this, "PlaystationRotation", playStation.transform.rotation);
                SaveLoad.WriteValue(this, "Controller1Position", playstationController1.transform.position);
                SaveLoad.WriteValue(this, "Controller1Rotation", playstationController1.transform.rotation);
                SaveLoad.WriteValue(this, "MemoryCard1Position", memoryCard1.transform.position);
                SaveLoad.WriteValue(this, "MemoryCard1Rotation", memoryCard1.transform.rotation);
                SaveLoad.WriteValue(this, "MemoryCard1Attached", memoryCard1.transform.parent == playStation.transform);
            }
            else
            {
                SaveLoad.WriteValue(this, "PlaystationUnpacked", false);
                SaveLoad.WriteValue(this, "PlaystationBoxPosition", _box.transform.position);
                SaveLoad.WriteValue(this, "PlaystationBoxRotation", _box.transform.rotation);
            }

            for (int i = 0; i < _memoryCards.Length; i++)
            {
                if (_memoryCards[i] != null)
                {
                    SaveLoad.WriteValue(this, $"MemoryCard{i:0}Purchased", true);
                    SaveLoad.WriteValue(this, $"MemoryCard{i:0}Position", _memoryCards[i].transform.position);
                    SaveLoad.WriteValue(this, $"MemoryCard{i:0}Rotation", _memoryCards[i].transform.rotation);
                }
                else SaveLoad.WriteValue(this, $"MemoryCard{i:0}Purchased", false);
            }

            for (int i = 0; i < _controllers.Length; i++)
            {
                if (_controllers[i] != null)
                {
                    SaveLoad.WriteValue(this, $"Controller{i:0}Purchased", true);
                    SaveLoad.WriteValue(this, $"Controller{i:0}Position", _controllers[i].transform.position);
                    SaveLoad.WriteValue(this, $"Controller{i:0}Rotation", _controllers[i].transform.rotation);
                }
                else SaveLoad.WriteValue(this, $"Controller{i:0}Purchased", false);
            }
        }

        private void UnpackBox()
        {
            SpawnConsoleFromBox();
            UnityEngine.Object.Destroy(_box);
            _unpackSound.Play();

        }

        private void SpawnConsoleFromBox()
        {
            playStation = UnityEngine.Object.Instantiate(_prefabPlayStation);

            playStation.transform.position = _box.transform.position;
            playStation.transform.rotation = Quaternion.identity;
            playStation.AddComponent<Rigidbody>();
            playStation.name = "PlayStation(itemx)";
            playStation.MakePickable();

            playstationController1 = UnityEngine.Object.Instantiate(_prefabPlayStationController);

            playstationController1.transform.position = _box.transform.position + Vector3.up * 0.5f;
            playstationController1.transform.rotation = Quaternion.identity;
            playstationController1.AddComponent<Rigidbody>();
            playstationController1.name = "PlayStation Controller(itemx)";
            playstationController1.MakePickable();

            memoryCard1 = UnityEngine.Object.Instantiate(_prefabMemoryCard);

            memoryCard1.transform.position = _box.transform.position + Vector3.up * 0.5f;
            memoryCard1.transform.rotation = Quaternion.identity;
            memoryCard1.transform.localScale = new Vector3(1f, 1f, 1f);
            memoryCard1.AddComponent<Rigidbody>();
            memoryCard1.name = "Memory Card(itemx)";
            memoryCard1.MakePickable();
        }

        private void Mod_OnLoad()
        {
            AssetBundle ab = LoadAssets.LoadBundle("PlayStation.Assets.playstation.unity3d");

            // Load prefabs for spawning later
            _prefabPlayStation = ab.LoadAsset<GameObject>("playstation_console.prefab");
            _prefabPlayStationController = ab.LoadAsset<GameObject>("playstation_controller.prefab");
            _prefabMemoryCard = ab.LoadAsset<GameObject>("playstation_memorycard.prefab");

            _boxPrefab = ab.LoadAsset<GameObject>("playstation_box.prefab");
            _prefabControllerDisplay = ab.LoadAsset<GameObject>("playstation_controller_display.prefab");
            _prefabMemoryCardDisplay = ab.LoadAsset<GameObject>("playstation_memorycard_display.prefab");
            _unpackSound = ab.LoadAsset<AudioSource>("cardboard_box.wav");

            LoadSaveFileOrShop(ab);

            // Manually construct paths
            string modsPath = Application.dataPath + "/PlayStation_Emulator";
            string playstationPath = modsPath + "/PlayStation";
            string coresPath = playstationPath + "/cores";

            retroArchPath = playstationPath + "/retroarch.exe";
            retroArchCorePath = coresPath + "/pcsx_rearmed_libretro.dll";
            biosbinPath = playstationPath + "/system";
            romsPath = playstationPath + "/Roms";
            biosPath = biosbinPath + "/scph-1002.bin";

            // Ensure directories exist
            if (!Directory.Exists(modsPath))
            {
                Directory.CreateDirectory(modsPath);
            }
            if (!Directory.Exists(playstationPath))
            {
                Directory.CreateDirectory(playstationPath);
            }
            if (!Directory.Exists(coresPath))
            {
                Directory.CreateDirectory(coresPath);
            }
            if (!Directory.Exists(romsPath))
            {
                Directory.CreateDirectory(romsPath);
            }
            ModConsole.Log("Finding or creating TV Screen...");
            tvScreen = GameObject.Find("YARD/Building/LIVINGROOM/TV/TVScreen");
            if (tvScreen == null)
            {
                ModConsole.Log("TV Screen not found. Creating a new one...");
                tvScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tvScreen.name = "TVScreen";
                tvScreen.transform.position = new Vector3(0, 1, 0); // Adjust position as needed
                tvScreen.transform.localScale = new Vector3(2, 1, 1); // Adjust scale as needed
            }
            ModConsole.Log("TV Screen found/created: " + tvScreen.name);

            // Create RenderTexture for emulator output
            renderTexture = new RenderTexture(640, 480, 16, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            // Create a new material for the TV screen
            tvScreenMaterial = new Material(Shader.Find("Standard"));
            tvScreenMaterial.mainTexture = renderTexture;

            // Apply the material to the TV screen
            Renderer tvRenderer = tvScreen.GetComponent<Renderer>();
            if (tvRenderer != null)
            {
                tvRenderer.material = tvScreenMaterial;
            }
            else
            {
                ModConsole.LogError("TV Screen does not have a Renderer component!");
            }

            // Check for RetroArch executable and core
            ModConsole.Log("Checking for RetroArch executable and core...");
            if (!File.Exists(retroArchPath))
            {
                ModConsole.LogError("RetroArch executable not found at: " + retroArchPath);
                return;
            }
            if (!File.Exists(retroArchCorePath))
            {
                ModConsole.LogError("PCSX-ReARMed core not found at: " + retroArchCorePath);
                return;
            }
            if (!File.Exists(biosPath))
            {
                ModConsole.LogError("BIOS file not found at: " + biosPath);
                return;
            }

            ModConsole.Log("Mod_OnLoad completed.");
            ab.Unload(false);
        }

        private void LoadSaveFileOrShop(AssetBundle ab)
        {
            // Load variables for purchased and unpacked
            hasPurchased = SaveLoad.ReadValue<bool>(this, "PlayStationPurchased");
            hasUnpacked = SaveLoad.ReadValue<bool>(this, "PlayStationUnpacked");
            // Perform action depending on what state we are in
            if (hasPurchased)
                if (hasUnpacked)
                    SpawnConsoleFromSaveFile(); // If it's purchased and unpacked, let's spawn the console
                else
                    SpawnBoxFromSaveFile(); // If it's purchased but not unpacked, spawn the box
            else
                CreateModsShopConsole(ab); // Create store box item when purchased

            CreateAdditionalMemoryCards(); // Spawn extra memory cards
            CreateAdditionalControllers(); // Spawn extra controllers
        }

        private void CreateAdditionalControllers()
        {
            // Here I use a for loop to prevent repetitive code, it repeats the code below for how long the controllers list is
            Shop shop = ModsShop.ModsShop.GetShopReference();
            for (int i = 0; i < _controllers.Length; i++)
            {
                if (!SaveLoad.ValueExists(this, $"ExtraController{i:0}Purchased"))
                {
                    CreateAdditionalControllers_CreateShopItem(i, shop);
                    continue;
                }

                if (SaveLoad.ReadValue<bool>(this, $"ExtraController{i:0}Purchased"))
                {
                    _controllers[i] = Object.Instantiate(_prefabPlayStationController);
                    _controllers[i].transform.position = SaveLoad.ReadValue<Vector3>(this, $"ExtraController{i:0}Position");
                    _controllers[i].transform.rotation =
                        SaveLoad.ReadValue<Quaternion>(this, $"ExtraController{i:0}Rotation");
                    _controllers[i].MakePickable();
                    _controllers[i].name = "PlayStation Controller(itemx)";
                }
                else
                {
                    CreateAdditionalControllers_CreateShopItem(i, shop);
                }
            }
        }

        private void CreateAdditionalControllers_CreateShopItem(int i, Shop shop)
        {
            int i1 = i;
            ItemDetails item = shop.CreateShopItem(this, $"additionalController{i}", "Controller", 549, false,
                obj =>
                {
                    _controllers[i1] = obj.gameObject;
                    _controllers[i1].MakePickable();
                    _controllers[i1].name = "PlayStation Controller(itemx)";
                }, _prefabPlayStationController, SpawnMethod.Instantiate);

            shop.AddDisplayItem(item, _prefabControllerDisplay, SpawnMethod.Instantiate);
        }

        private void CreateAdditionalMemoryCards()
        {
            // Here I use a for loop to prevent repetitive code, it repeats the code below for how long the memory cards list is
            for (int i = 0; i < _memoryCards.Length; i++)
            {
                if (!SaveLoad.ValueExists(this, $"ExtraMemoryCard{i:0}Purchased"))
                {
                    CreateAdditionalMemoryCards_CreateShopItem(i);
                    continue;
                }

                if (SaveLoad.ReadValue<bool>(this, $"ExtraMemoryCard{i:0}Purchased"))
                {
                    _memoryCards[i] = Object.Instantiate(_prefabMemoryCard);
                    _memoryCards[i].transform.position = SaveLoad.ReadValue<Vector3>(this, $"ExtraMemoryCard{i:0}Position");
                    _memoryCards[i].transform.rotation =
                        SaveLoad.ReadValue<Quaternion>(this, $"ExtraMemoryCard{i:0}Rotation");
                    _memoryCards[i].MakePickable();
                    _memoryCards[i].name = "Memory Card(itemx)";
                }
                else
                {
                    CreateAdditionalMemoryCards_CreateShopItem(i);
                }
            }
        }

        private void CreateAdditionalMemoryCards_CreateShopItem(int i)
        {
            Shop shop = ModsShop.ModsShop.GetShopReference();
            int i1 = i;
            ItemDetails item = shop.CreateShopItem(this, $"additionalMemoryCard{i}", "Memory Card", 199, false,
                obj =>
                {
                    _memoryCards[i1] = obj.gameObject;
                    _memoryCards[i1].MakePickable();
                    _memoryCards[i1].name = "Memory Card(itemx)";
                }, _prefabMemoryCard, SpawnMethod.Instantiate);

            shop.AddDisplayItem(item, _prefabMemoryCardDisplay, SpawnMethod.Instantiate);
        }

        private void SpawnBoxFromSaveFile()
        {
            // Spawn box and load save file position and rotation
            _box = Object.Instantiate(_boxPrefab);
            _box.transform.position = SaveLoad.ReadValue<Vector3>(this, "PlaystationBoxPosition");
            _box.transform.rotation = SaveLoad.ReadValue<Quaternion>(this, "PlaystationBoxRotation");
            _box.MakePickable();
            _box.name = "playstation box(itemx)";
        }

        private void SpawnConsoleFromSaveFile()
        {
            // Spawn console and load position and rotation
            playStation = Object.Instantiate(_prefabPlayStation);
            playStation.transform.position = SaveLoad.ReadValue<Vector3>(this, "PlaystationPosition");
            playStation.transform.rotation = SaveLoad.ReadValue<Quaternion>(this, "PlaystationRotation");
            playStation.AddComponent<Rigidbody>();
            playStation.name = "PlayStation(itemx)";
            playStation.MakePickable();

            // Spawn controller and load position and rotation
            playstationController1 = Object.Instantiate(_prefabPlayStationController);
            playstationController1.transform.position =
                SaveLoad.ReadValue<Vector3>(this, "Controller1Position");
            playstationController1.transform.rotation =
                SaveLoad.ReadValue<Quaternion>(this, "Controller1Rotation");
            playstationController1.AddComponent<Rigidbody>();
            playstationController1.name = "PlayStation Controller(itemx)";
            playstationController1.MakePickable();

            // Spawn memory card and load position and rotation
            memoryCard1 = Object.Instantiate(_prefabMemoryCard);
            memoryCard1.transform.position = SaveLoad.ReadValue<Vector3>(this, "MemoryCard1Position");
            memoryCard1.transform.rotation = SaveLoad.ReadValue<Quaternion>(this, "MemoryCard1Rotation");
            memoryCard1.transform.localScale = new Vector3(1f, 1f, 1f);
            memoryCard1.AddComponent<Rigidbody>();
            memoryCard1.name = "Memory Card(itemx)";
            memoryCard1.MakePickable();
        }

        private void CreateModsShopConsole(AssetBundle ab)
        {
            // Create box in shop
            Shop shop = ModsShop.ModsShop.GetShopReference();
            GameObject boxV = ab.LoadAsset<GameObject>("playstation_box_view.prefab");
            ItemDetails myItem = shop.CreateShopItem(this, "playstation", "PlayStation", 5279f, false,
                AfterPurchased,
                _boxPrefab, SpawnMethod.Instantiate);
            // Rotate the item 90 degrees so it looks nicer
            shop.AddDisplayItem(myItem, boxV, SpawnMethod.Instantiate, Vector3.zero);
        }

        private void AfterPurchased(Checkout obj)
        {
            // Set purchased state to true
            hasPurchased = true;
            // The ModsShop mod will spawn our box, we just set up the things we need
            _box = obj.gameObject;
            _box.MakePickable();
            _box.name = "playstation box(itemx)";
        }

        private Camera _main;
        private GameObject _boxPrefab;

        private void Mod_Update()
        {
            // Find the main camera
            if (_main == null)
            {
                _main = Camera.main;
                return; // Let's prevent the rest of the code from running if the camera isn't found
            }

            // Interaction raycast
            // Interaction raycast
            foreach (RaycastHit hit in Physics.RaycastAll(_main.transform.position, _main.transform.forward, 3))
            {
                // Check if the player is looking at the PlayStation box
                if (hit.collider.gameObject.name == "playstation box(itemx)")
                {
                    FsmVariables.GlobalVariables.GetFsmBool("GUIuse").Value = true;
                    FsmVariables.GlobalVariables.GetFsmString("GUIinteraction").Value = "UNPACK";
                    // When F is pressed, unpack the box
                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        hasUnpacked = true;
                        UnpackBox();
                    }
                }

                // Check if the player is looking at a memory card
                if (hit.collider.gameObject.name == "Memory Card(itemx)")
                {
                    FsmVariables.GlobalVariables.GetFsmBool("GUIuse").Value = true;
                    FsmVariables.GlobalVariables.GetFsmString("GUIinteraction").Value = "ATTACH";

                    // When F is pressed, attach the memory card to the PlayStation
                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        AttachMemoryCard(hit.collider.gameObject);
                    }

                    if (hit.collider.gameObject.name == "PlayStation(itemx)")
                    {
                        FsmVariables.GlobalVariables.GetFsmBool("GUIuse").Value = true;
                        FsmVariables.GlobalVariables.GetFsmString("GUIinteraction").Value = "PLAY";

                        // When F is pressed, start the emulator
                        if (Input.GetKeyDown(KeyCode.F))
                        {
                            StartRetroArch();
                        }
                    }
                }
            }
        }


        private void AttachMemoryCard(GameObject memoryCard)
        {
            if (playStation == null) return;

            // Check if the memory card is near the PlayStation console
            float distance = Vector3.Distance(memoryCard.transform.position, playStation.transform.position);
            if (distance > 0.5f) return; // Adjust distance as needed

            // Attach the memory card to the console
            memoryCard.transform.parent = playStation.transform;
            memoryCard.transform.localPosition = new Vector3(0, 0.1f, 0); // Adjust position as needed
            memoryCard.transform.localRotation = Quaternion.identity;

            // Disable the memory card's rigidbody and collider
            Rigidbody rb = memoryCard.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider col = memoryCard.GetComponent<Collider>();
            if (col != null) col.enabled = false;


            ModConsole.Log("Memory card attached to PlayStation!");
        }


        private void StartRetroArch()
        {
            if (retroArchProcess != null && !retroArchProcess.HasExited)
            {
                ModConsole.Log("RetroArch is already running!");
                return;
            }

            // Start RetroArch process
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = retroArchPath,
                Arguments = $"-L \"{retroArchCorePath}\" -f --config \"{Application.dataPath}/Mods/PlayStation/retroarch.cfg\" \"{romsPath}/game.cue\"",    
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            retroArchProcess = new Process { StartInfo = startInfo };
            retroArchProcess.Start();

            ModConsole.Log("RetroArch started!");
        }

        private void Mod_OnGUI()
        {
            // Draw the emulator output on the TV screen
            if (retroArchProcess != null && !retroArchProcess.HasExited && renderTexture != null)
            {
                // Render the emulator output to the RenderTexture
                Graphics.Blit(null, renderTexture);
            }
        }

    }
}

