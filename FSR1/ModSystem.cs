// MIT License

// Copyright(c) 2022 George Tokmaji

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace VSMods.FSR1
{
    public static class Initializer
    {
        public const string HarmonyID = "org.github.fulgen301.vsmods.fsr1";
        public const int RequiredGLMajor = 4;
        public const int RequiredGLMinor = 2;
        public const string RequiredGLSLVersion = "420";

        public static bool HighEnoughGLContextVersion
        {
            get
            {
                var parts = ClientSettings.GlContextVersion.Split('.');
                return Convert.ToInt32(parts[0]) >= RequiredGLMajor && Convert.ToInt32(parts[1]) >= RequiredGLMinor;
            }
        }

        public static bool HighEnoughGLSLVersion => false && ShaderRegistry.IsGLSLVersionSupported(RequiredGLSLVersion);

        public static bool Supported => HighEnoughGLContextVersion;

        private static Harmony harmony;

        private static readonly Dictionary<string, string> stringTable = new()
        {
            { "setting-name-easu", "EASU" },
            { "setting-hover-easu", "Enables EASU (Edge-Adaptive Spatial Upscaling)." },
            { "setting-name-rcas", "RCAS" },
            { "setting-hover-rcas", "Enables RCAS (Robust Contrast-Adaptive Sharpening)." }
        };

        [ModuleInitializer]
        public static void Initialize()
        {
            if (!HighEnoughGLSLVersion)
            {
                ClientSettings.Inst.Bool["easu"] = false;
                ClientSettings.Inst.Bool["rcas"] = false;
            }

            harmony = new(HarmonyID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            var loadEntry = AccessTools.DeclaredMethod(typeof(TranslationService), "LoadEntry");
            var translationService = (TranslationService) Lang.AvailableLanguages[Lang.DefaultLocale];

            foreach (var pair in stringTable)
            {
                loadEntry.Invoke(translationService, new object[] { pair, GlobalConstants.DefaultDomain });
            }
        }
    }

    public class TranslationOrigin : IAssetOrigin
    {
        public static TranslationOrigin Instance = new();
        public string OriginPath => Assembly.GetExecutingAssembly().Location;

        private TranslationOrigin()
        {
        }

        public List<IAsset> GetAssets(AssetCategory category, bool shouldLoad = true)
        {
            if (category == AssetCategory.lang)
            {
                return new() { new Translation() };
            }

            return new();
        }

        public List<IAsset> GetAssets(AssetLocation baseLocation, bool shouldLoad = true)
        {
            return new();
        }

        public bool IsAllowedToAffectGameplay()
        {
            return false;
        }

        public void LoadAsset(IAsset asset)
        {
        }

        public bool TryLoadAsset(IAsset asset)
        {
            return true;
        }
    }

    public class Translation : IAsset
    {
        public string Name => Lang.DefaultLocale + ".json";

        public AssetLocation Location => new("game", "");

        public IAssetOrigin Origin { get => TranslationOrigin.Instance; set => throw new NotImplementedException(); }
        public byte[] Data { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool IsLoaded() => true;

        public BitmapRef ToBitmap(ICoreClientAPI capi) => throw new NotImplementedException();

        public T ToObject<T>(Newtonsoft.Json.JsonSerializerSettings settings = null) => throw new NotImplementedException();

        public string ToText()
        {
            return """
{
    "setting-name-easu" : "EASU",
    "setting-hover-easu" : "Enables EASU (Edge-Adaptive Spatial Upscaling).",
    "setting-name-rcas" : "RCAS",
    "setting-hover-rcas" : "Enables RCAS (Robust Contrast-Adaptive Sharpening)."
}
""";
        }
    }

    public class SystemFSR1 : ModSystem
    {
        private ICoreClientAPI capi;
        private FSR1Renderer fsr1;

        public static FSR1Renderer Renderer { get; private set; }

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            ((ICoreClientAPI) api).Assets.Origins.Add(TranslationOrigin.Instance);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            Renderer = fsr1 = new(capi);
            //api.Event.RegisterRenderer(fsr1, FSR1Renderer.RenderStage, "FSR1");
        }

        public override void Dispose()
        {
            if (fsr1 is not null)
            {
                fsr1.Dispose();
                Renderer = fsr1 = null;
            }
        }
    }

    [HarmonyPatch(typeof(ClientPlatformWindows), "BlitPrimaryToDefault")]
    static class ClientPlatformWindowsPatch
    {
        public static bool Prefix()
        {
            if (SystemFSR1.Renderer is not null)
            {
                SystemFSR1.Renderer.OnRenderFrame();
                return false;
            }

            return true;
        }
    }
}