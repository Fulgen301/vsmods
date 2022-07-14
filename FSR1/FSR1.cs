
using HarmonyLib;

using OpenTK.Graphics.OpenGL;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace VSMods.FSR1
{
    public class FSR1Renderer : IRenderer
    {
        public double RenderOrder => 1;

        public int RenderRange => 1;
        public const EnumRenderStage RenderStage = EnumRenderStage.AfterFinalComposition;

        public bool Easu
        {
            get
            {
                return ClientSettings.Inst.Bool["easu"];
            }
            set
            {
                ClientSettings.Inst.Bool["easu"] = value;
            }
        }

        public bool Rcas
        {
            get
            {
                return ClientSettings.Inst.Bool["rcas"];
            }
            set
            {
                ClientSettings.Inst.Bool["rcas"] = value;
            }
        }

        private readonly ICoreClientAPI capi;
        private IShaderProgram easuProgram;
        private IShaderProgram rcasProgram;
        private FrameBufferRef easuFrameBuffer;

        private const string VertexShaderSource = """
#version 420
layout(location=0) in vec2 position;

out vec2 texCoord;

void main(void)
{
	float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);
    gl_Position = vec4(x, y, 0, 1);
    texCoord = vec2((x+1.0) * 0.5, (y + 1.0) * 0.5);
}
""";

        public FSR1Renderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            capi.Event.ReloadShader += LoadShader;
            ClientSettings.Inst.Float.AddWatcher("SSAA", newValue => UpdateSourceSize(ScreenManager.Platform.WindowSize));
        }

        public void Dispose()
        {
            easuProgram?.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage) => OnRenderFrame();

        public void OnRenderFrame()
        {
            var platform = (ClientPlatformWindows) ScreenManager.Platform;
            var screenQuad = (MeshRef) AccessTools.DeclaredField(typeof(ClientPlatformWindows), "screenQuad").GetValue(platform);

            FrameBufferRef sourceFrameBuffer = platform.FrameBuffers[(int) EnumFrameBuffer.Primary];
            FrameBufferRef targetFrameBuffer = Rcas ? easuFrameBuffer : null;

            void Draw(IShaderProgram shaderProgram)
            {
                shaderProgram.Use();
                ((ShaderProgramBase) shaderProgram).BindTexture2D("textureSampler", sourceFrameBuffer.ColorTextureIds[0]);
                platform.RenderFullscreenTriangle(screenQuad);
                shaderProgram.Stop();
            }

            void LoadFrameBuffer(FrameBufferRef frameBuffer)
            {
                if (frameBuffer is not null)
                {
                    platform.LoadFrameBuffer(frameBuffer);
                }
                else
                {
                    platform.LoadFrameBuffer(EnumFrameBuffer.Default);
                }
            }

            LoadFrameBuffer(targetFrameBuffer);
            if (targetFrameBuffer is not null)
            {
                GL.DrawBuffers(1, new[] { DrawBuffersEnum.ColorAttachment0 });
                GL.Disable(EnableCap.DepthTest);
                platform.GlToggleBlend(true);
            }

            if (Easu)
            {
                Draw(easuProgram);
            }
            else
            {
                ShaderProgramBlit program = ShaderPrograms.Blit;
                program.Use();
                program.Scene2D = sourceFrameBuffer.ColorTextureIds[0];
                platform.RenderFullscreenTriangle(screenQuad);
                program.Stop();
            }

            if (Rcas)
            {
                sourceFrameBuffer = targetFrameBuffer;
                LoadFrameBuffer(null);

                GL.Viewport(0, 0, platform.WindowSize.Width, platform.WindowSize.Height);

                Draw(rcasProgram);
            }
        }

        private bool LoadShader()
        {
            easuProgram?.Dispose();
            rcasProgram?.Dispose();

            IShader vertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            vertexShader.Code = VertexShaderSource;

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("#define A_GPU 1");
            stringBuilder.AppendLine("#define A_GLSL 1");

            void ReadStream(Stream stream)
            {
                using StreamReader reader = new(stream);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    stringBuilder.AppendLine(line);
                }
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FSR1.ffx_a.h"))
            {
                ReadStream(stream);
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FSR1.ffx_fsr1.h"))
            {
                ReadStream(stream);
            }

            Size2i windowSize = ScreenManager.Platform.WindowSize;

            LoadEASU(vertexShader, stringBuilder, windowSize);
            LoadRCAS(vertexShader, stringBuilder, windowSize);
            LoadFrameBuffer(windowSize);

            return true;
        }

        private void LoadEASU(IShader vertexShader, StringBuilder fragmentShaderPrefixCode, Size2i windowSize)
        {
            easuProgram = capi.Shader.NewShaderProgram();

            IShader fragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            fragmentShader.PrefixCode = "#define FSR_EASU_F 1\n" + fragmentShaderPrefixCode.ToString();

            fragmentShader.Code = """
#version 420

layout(location = 0) out vec4 fragColor;
//layout(origin_upper_left) in vec4 gl_FragCoord;

in vec2 texCoord;

uniform uvec2 sourceSize;
uniform uvec2 targetSize;
uniform sampler2D textureSampler;

AF4 FsrEasuRF(AF2 p) { return AF4(textureGather(textureSampler, p, 0)); }
AF4 FsrEasuGF(AF2 p) { return AF4(textureGather(textureSampler, p, 1)); }
AF4 FsrEasuBF(AF2 p) { return AF4(textureGather(textureSampler, p, 2)); }

void main(void)
{
    AU4 const0;
    AU4 const1;
    AU4 const2;
    AU4 const3;
   
    FsrEasuCon(
        const0,
        const1,
        const2,
        const3,
        sourceSize.x,
        sourceSize.y,
        sourceSize.x,
        sourceSize.y,
        targetSize.x,
        targetSize.y
    );

	AF3 color = AF3(0, 0, 0);
	AU2 position = AU2(texCoord * targetSize);
	FsrEasuF(color, position, const0, const1, const2, const3);
	fragColor = vec4(color, 1.0);
}
""";
            easuProgram.VertexShader = vertexShader;
            easuProgram.FragmentShader = fragmentShader;
            easuProgram.Compile();

            int programId = ((ShaderProgramBase) easuProgram).ProgramId;
            GL.UseProgram(programId);

            var width = (uint) windowSize.Width;
            var height = (uint) windowSize.Height;

            GL.Uniform2(GL.GetUniformLocation(programId, "sourceSize"), (uint) (width * ClientSettings.SSAA), (uint) (height * ClientSettings.SSAA));
            GL.Uniform2(GL.GetUniformLocation(programId, "targetSize"), width, height);
            GL.UseProgram(0);

            string name = "FSR1 - EASU";
            GL.ObjectLabel(ObjectLabelIdentifier.Program, programId, name.Length, name);
        }

        private void LoadRCAS(IShader vertexShader, StringBuilder fragmentShaderPrefixCode, Size2i windowSize)
        {
            rcasProgram = capi.Shader.NewShaderProgram();

            IShader fragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);

            fragmentShader.PrefixCode = "#define FSR_RCAS_F 1\n" + fragmentShaderPrefixCode.ToString();

            fragmentShader.Code = """
#version 420

layout(location = 0) out vec4 fragColor;

in vec2 texCoord;

uniform float sharpening;
uniform uvec2 targetSize;
uniform sampler2D textureSampler;

AF4 FsrRcasLoadF(ASU2 p) { return AF4(texelFetch(textureSampler, p, 0)); }
void FsrRcasInputF(inout AF1 r, inout AF1 g, inout AF1 b) {}

void main(void)
{
    AU4 const0;
    FsrRcasCon(const0, sharpening);
    AU2 position = AU2(texCoord * targetSize);

    AF3 gamma2Color = AF3(0, 0, 0);
    FsrRcasF(gamma2Color.r, gamma2Color.g, gamma2Color.b, position, const0);

    fragColor = vec4(gamma2Color, 1.0);
}
""";

            rcasProgram.VertexShader = vertexShader;
            rcasProgram.FragmentShader = fragmentShader;
            rcasProgram.Compile();

            int programId = ((ShaderProgramBase) rcasProgram).ProgramId;
            GL.UseProgram(programId);

            UpdateSourceSize(windowSize);
            GL.Uniform2(GL.GetUniformLocation(programId, "targetSize"), (uint) windowSize.Width, (uint) windowSize.Height);
            GL.Uniform1(GL.GetUniformLocation(programId, "sharpening"), 0.2f);
            GL.UseProgram(0);

            string name = "FSR1 - RCAS";
            GL.ObjectLabel(ObjectLabelIdentifier.Program, programId, name.Length, name);
        }

        private void LoadFrameBuffer(Size2i windowSize)
        {
            if (easuFrameBuffer is not null)
            {
                ScreenManager.Platform.DisposeFrameBuffer(easuFrameBuffer, true);
            }

            easuFrameBuffer = ScreenManager.Platform.CreateFramebuffer(new FramebufferAttrs("FSR1", ScreenManager.Platform.WindowSize.Width, ScreenManager.Platform.WindowSize.Height)
            {
                Attachments = new FramebufferAttrsAttachment[]
                {
                    new FramebufferAttrsAttachment() {
                        AttachmentType = EnumFramebufferAttachment.ColorAttachment0,
                        Texture = new RawTexture()
                        {
                            Width = windowSize.Width,
                            Height = windowSize.Height,
                            PixelFormat = EnumTexturePixelFormat.Rgba,
                            PixelInternalFormat = EnumTextureInternalFormat.Rgba8
                        }
                    },
                    new FramebufferAttrsAttachment()
                    {
                        AttachmentType = EnumFramebufferAttachment.DepthAttachment,
                        Texture = new RawTexture()
                        {
                            Width = windowSize.Width,
                            Height = windowSize.Height,
                            PixelFormat = EnumTexturePixelFormat.DepthComponent,
                            PixelInternalFormat = EnumTextureInternalFormat.DepthComponent32
                        }
                    }
                }
            });
        }

        private void UpdateSourceSize(Size2i windowSize)
        {
            if (easuProgram is not null)
            {
                GL.Uniform2(GL.GetUniformLocation(((ShaderProgramBase) easuProgram).ProgramId, "sourceSize"), (uint) (((uint) windowSize.Width) * ClientSettings.SSAA), (uint) ((uint) (windowSize.Height) * ClientSettings.SSAA));
            }
        }
    }
}