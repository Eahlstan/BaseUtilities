﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTKUtils;
using OpenTKUtils.Common;
using OpenTKUtils.GL4;
using System;
using System.Drawing;
using System.Windows.Forms;
using BaseUtils;
using System.Collections.Generic;

// Demonstrate the volumetric calculations needed to compute a plane facing the user inside a bounding box done inside a geo shader
// this one add on tex coord calculation and using a single tight quad shows its working

namespace TestOpenTk
{
    public partial class ShaderDynamicGrid : Form
    {
        private OpenTKUtils.WinForm.GLWinFormControl glwfc;
        private Controller3D gl3dcontroller;

        private Timer systemtimer = new Timer();

        public ShaderDynamicGrid()
        {
            InitializeComponent();

            glwfc = new OpenTKUtils.WinForm.GLWinFormControl(glControlContainer);

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////


        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
        }

        private void ControllerDraw(GLMatrixCalc mc, long time)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).Set(gl3dcontroller.MatrixCalc);        // set the matrix unform block to the controller 3d matrix calc.

            DynamicGridShader s = items.PLShader("PLGRIDVertShader") as DynamicGridShader;
            IGLRenderableItem i = rObjects["DYNGRIDRENDER"];
            i.InstanceCount = s.ComputeUniforms(gl3dcontroller.MatrixCalc, out int gridwidth, out Vector3 start);
            DynamicGridBitmapShader bs = items.PLShader("PLGRIDBitmapVertShader") as DynamicGridBitmapShader;
            bs.ComputeUniforms(gridwidth, start);

            solmarker.Position = gl3dcontroller.MatrixCalc.TargetPosition;
            solmarker.Scale = gl3dcontroller.MatrixCalc.EyeDistance / 20;

            rObjects.Render(glwfc.RenderState, gl3dcontroller.MatrixCalc);

            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.TargetPosition + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " dir " + gl3dcontroller.Camera.Current + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance + " Zoom " + gl3dcontroller.Zoom.Current;
        }

        public class DynamicGridShader : GLShaderPipelineShadersBase
        {
            public int ComputeUniforms(GLMatrixCalc mc, out int gridwidth, out Vector3 start)
            {
                int lines = 21;
                gridwidth = 10000;

                int sy = (int)ObjectExtensionsNumbersBool.Clamp(mc.TargetPosition.Y, -2000, 2000);

                if (mc.EyeDistance >= 10000)
                {
                    start = new Vector3(-50000, sy, -20000);
                }
                else
                {
                    int horzlines = 321;
                    gridwidth = 10;

                    if (mc.EyeDistance >= 1000)
                    {
                        horzlines = 81;
                        gridwidth = 1000;
                    }
                    else if (mc.EyeDistance >= 100)
                    {
                        gridwidth = 100;
                    }

                    lines = horzlines * 2;

                    int gridstart = (horzlines - 1) * gridwidth / 2;
                    int width = (horzlines - 1) * gridwidth;

                    int sx = (int)(mc.TargetPosition.X) / gridwidth * gridwidth - gridstart;
                    if (sx < -50000)
                        sx = -50000;
                    else if (sx + width > 50000)
                        sx = 50000 - width;

                    int sz = (int)(mc.TargetPosition.Z) / gridwidth * gridwidth - gridstart;
                    if (sz < -20000)
                        sz = -20000;
                    else if (sz + width > 70000)
                        sz = 70000 - width;
                    start = new Vector3(sx, sy, sz);
                }

                GL.ProgramUniform1(this.Id, 10, lines);
                GL.ProgramUniform1(this.Id, 11, gridwidth);
                GL.ProgramUniform3(this.Id, 12, ref start);
                GLStatics.Check();

                return lines;
            }

            string vcode()
            { return @"
#version 450 core

layout (location=10) uniform int lines;
layout (location=11) uniform int gridwidth;
layout (location=12) uniform vec3 start;

#include OpenTKUtils.GL4.UniformStorageBlocks.matrixcalc.glsl

out gl_PerVertex {
        vec4 gl_Position;
        float gl_PointSize;
        float gl_ClipDistance[];
    };

out vec4 vs_color;

void main(void)
{
    int line = gl_InstanceID;
    int linemod = gl_VertexID;
    float dist = mc.EyeDistance;

    int horzlines = lines/2;
    int width = (horzlines-1)*gridwidth;
    if ( gridwidth==10000 && line < horzlines)
        width = 100000;

    int lpos;
    vec4 position;

    if ( line>= horzlines) // vertical
    {
        line -= horzlines;
        lpos = int(start.x) + line * gridwidth;
        position = vec4( lpos , start.y, clamp(start.z + width * linemod,-20000,70000), 1);
    }
    else    
    {
        lpos = int(start.z) + gridwidth * line;
        position = vec4( clamp(start.x + width * linemod,-50000,50000), start.y, lpos , 1);
    }

    gl_Position = mc.ProjectionModelMatrix * position;        // order important

    float a=1;
    float b = 0.7;

    if ( gridwidth != 10000 ) 
    {
        if ( abs(lpos) % (10*gridwidth) != 0 )
        {
            a =  1.0 - clamp((dist - gridwidth)/float(10*gridwidth),0,0.95f);
            b = 0.5;
        }
    }
                
    vs_color = vec4(color.x*b,color.y*b,color.z*b,a);
}
"; }

            public DynamicGridShader(Color c)
            {
                CompileLink(OpenTK.Graphics.OpenGL4.ShaderType.VertexShader, vcode(), new object[] { "color", c }, completeoutfile:@"c:\code\sh.txt");
            }

        }

        public class DynamicGridBitmapShader : GLShaderPipelineShadersBase
        {
            public void ComputeUniforms(int gridwidth, Vector3 start)
            {
                GL.ProgramUniform1(this.Id, 11, gridwidth);
                GL.ProgramUniform3(this.Id, 12, ref start);
            }

            string vcode()
            { return @"
#version 450 core

layout (location=11) uniform int gridwidth;
layout (location=12) uniform vec3 start;

#include OpenTKUtils.GL4.UniformStorageBlocks.matrixcalc.glsl

out gl_PerVertex {
        vec4 gl_Position;
        float gl_PointSize;
        float gl_ClipDistance[];
    };

out vec4 vs_color;

void main(void)
{
    int bitmap = gl_InstanceID;
    int vid = gl_VertexID;
    float dist = mc.EyeDistance;

    int majorlines = clamp(gridwidth*5,0,10000);

    int sy = int(clamp(mc.TargetPosition.y,-2000,2000));

    int sx = int(clamp(mc.TargetPosition.x - majorlines,-50000,50000-majorlines*2)) + 50000;  //move to positive rep so rounding next is always down

    if ( sx % majorlines > majorlines/2)                // if we are over 1/2 way across, move over
        sx += majorlines;

    sx = sx / majorlines * majorlines - 50000;         // round and adjust back

    int sz = int(clamp(mc.TargetPosition.z,-20000,70000-majorlines*2)) + 50000;  //move to positive rep so rounding next is always down

    sz = sz / majorlines * majorlines - 50000;         // round and adjust back

    sx += majorlines *(bitmap/3);
    sz += majorlines *(bitmap%3);

    int textwidth = int(clamp(dist/3,0,2500));
    sx +=(vid/2)*textwidth;
    sz += (1-vid%2)*textwidth/3;

    gl_Position = mc.ProjectionModelMatrix * vec4(sx,sy,sz,1);        // order important


    vs_color = vec4(vid*0.2+0.2,0.0,0,1.0-0.3*(bitmap%3));
}
"; }

            public DynamicGridBitmapShader()
            {
                CompileLink(OpenTK.Graphics.OpenGL4.ShaderType.VertexShader, vcode());
            }
        }


        public class GLFixedShader : GLShaderPipeline
        {
            public GLFixedShader(Color c, Action<IGLProgramShader> action = null) : base(action)
            {
                AddVertexFragment(new GLPLVertexShaderWorldCoord(), new GLPLFragmentShaderFixedColour(c));
            }
        }

        GLRenderDataTranslationRotationTexture solmarker;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            gl3dcontroller = new Controller3D();
            gl3dcontroller.MatrixCalc.PerspectiveNearZDistance = 1f;
            gl3dcontroller.MatrixCalc.PerspectiveFarZDistance = 500000f;
            gl3dcontroller.ZoomDistance = 5000F;
            gl3dcontroller.EliteMovement = true;
            gl3dcontroller.PaintObjects = ControllerDraw;

            gl3dcontroller.KeyboardTravelSpeed = (ms,eyedist) =>
            {
                return (float)ms * 1.0f * Math.Min(eyedist/1000,10);
            };

            gl3dcontroller.Zoom.ZoomFact = 1.1f;

            gl3dcontroller.MatrixCalc.InPerspectiveMode = true;
            gl3dcontroller.Start(glwfc, new Vector3(0, 0, 0), new Vector3(140.75f, 0, 0), 0.5F);

            items.Add("MCUB", new GLMatrixCalcUniformBlock());     // create a matrix uniform block 

            int front = -20000, back = front + 90000, left = -45000, right = left + 90000, vsize = 2000;
 
            Vector4[] displaylines = new Vector4[]
            {
                new Vector4(left,-vsize,front,1),   new Vector4(left,+vsize,front,1),
                new Vector4(left,+vsize,front,1),      new Vector4(right,+vsize,front,1),
                new Vector4(right,+vsize,front,1),     new Vector4(right,-vsize,front,1),
                new Vector4(right,-vsize,front,1),  new Vector4(left,-vsize,front,1),

                new Vector4(left,-vsize,back,1),    new Vector4(left,+vsize,back,1),
                new Vector4(left,+vsize,back,1),       new Vector4(right,+vsize,back,1),
                new Vector4(right,+vsize,back,1),      new Vector4(right,-vsize,back,1),
                new Vector4(right,-vsize,back,1),   new Vector4(left,-vsize,back,1),

                new Vector4(left,-vsize,front,1),   new Vector4(left,-vsize,back,1),
                new Vector4(left,+vsize,front,1),      new Vector4(left,+vsize,back,1),
                new Vector4(right,-vsize,front,1),  new Vector4(right,-vsize,back,1),
                new Vector4(right,+vsize,front,1),     new Vector4(right,+vsize,back,1),
            };

            {
                items.Add("LINEYELLOW", new GLFixedShader(System.Drawing.Color.Yellow));
                GLRenderControl rl = GLRenderControl.Lines(1);
                rObjects.Add(items.Shader("LINEYELLOW"), GLRenderableItem.CreateVector4(items, rl, displaylines));
            }


            {
                items.Add("solmarker", new GLTexture2D(Properties.Resources.dotted));
                items.Add("TEX", new GLTexturedShaderWithObjectTranslation());
                GLRenderControl rq = GLRenderControl.Quads(cullface: false);
                solmarker = new GLRenderDataTranslationRotationTexture(items.Tex("solmarker"), new Vector3(0, 0, 0));
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, rq,
                             GLShapeObjectFactory.CreateQuad(1.0f, 1.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexQuad,
                             solmarker
                             ));
            }



            {
                items.Add("PLGRIDVertShader", new DynamicGridShader(Color.Cyan));
                items.Add("PLGRIDFragShader", new GLPLFragmentShaderColour());

                GLRenderControl rl = GLRenderControl.Lines(1);
                rl.DepthTest = false;

                items.Add("DYNGRID", new GLShaderPipeline(items.PLShader("PLGRIDVertShader"), items.PLShader("PLGRIDFragShader")));
                rObjects.Add(items.Shader("DYNGRID"), "DYNGRIDRENDER", GLRenderableItem.CreateNullVertex(rl, dc: 2));

            }

            {
                items.Add("PLGRIDBitmapVertShader", new DynamicGridBitmapShader());
                items.Add("PLGRIDBitmapFragShader", new GLPLFragmentShaderColour());

                GLRenderControl rl = GLRenderControl.TriStrip(cullface: false);
                rl.DepthTest = false;

                items.Add("DYNGRIDBitmap", new GLShaderPipeline(items.PLShader("PLGRIDBitmapVertShader"), items.PLShader("PLGRIDBitmapFragShader")));

                rObjects.Add(items.Shader("DYNGRIDBitmap"), "DYNGRIDBitmapRENDER", GLRenderableItem.CreateNullVertex(rl, dc: 4, ic:9));
            }


















            //Bitmap[] numbitmaps = new Bitmap[116];

            //{
            //    Font fnt = new Font("Arial", 20);
            //    for (int i = 0; i < numbitmaps.Length; i++)
            //    {
            //        int v = -45000 + 1000 * i;      // range from -45000 to +70000 
            //        numbitmaps[i] = new Bitmap(100, 100);
            //        BaseUtils.BitMapHelpers.DrawTextCentreIntoBitmap(ref numbitmaps[i], v.ToString(), fnt, Color.Red, Color.AliceBlue);
            //    }

            //    GLTexture2DArray numtextures = new GLTexture2DArray(numbitmaps, ownbitmaps: true);
            //    items.Add("Nums", numtextures);

            //    Matrix4[] numberposx = new Matrix4[(right - left) / 1000 + 1];
            //    for (int i = 0; i < numberposx.Length; i++)
            //    {
            //        numberposx[i] = Matrix4.CreateScale(1);
            //        numberposx[i] *= Matrix4.CreateRotationX(-25f.Radians());
            //        numberposx[i] *= Matrix4.CreateTranslation(new Vector3(left + 1000 * i, 0, front));
            //    }

            //    GLShaderPipeline numshaderx = new GLShaderPipeline(new GLPLVertexShaderTextureModelCoordWithMatrixTranslation(), new GLPLFragmentShaderTexture2DIndexed(0));
            //    items.Add("IC-X", numshaderx);

            //    GLRenderControl rq = GLRenderControl.Quads(cullface: false);
            //    GLRenderDataTexture rt = new GLRenderDataTexture(items.Tex("Nums"));

            //    rObjects.Add(numshaderx, "xnum",
            //                            GLRenderableItem.CreateVector4Vector2Matrix4(items, rq,
            //                                    GLShapeObjectFactory.CreateQuad(500.0f), GLShapeObjectFactory.TexQuad, numberposx,
            //                                    rt, numberposx.Length));

            //    Matrix4[] numberposz = new Matrix4[(back - front) / 1000 + 1];
            //    for (int i = 0; i < numberposz.Length; i++)
            //    {
            //        numberposz[i] = Matrix4.CreateScale(1);
            //        numberposz[i] *= Matrix4.CreateRotationX(-25f.Radians());
            //        numberposz[i] *= Matrix4.CreateTranslation(new Vector3(right + 1000, 0, front + 1000 * i));
            //    }

            //    GLShaderPipeline numshaderz = new GLShaderPipeline(new GLPLVertexShaderTextureModelCoordWithMatrixTranslation(), new GLPLFragmentShaderTexture2DIndexed(25));
            //    items.Add("IC-Z", numshaderz);

            //    rObjects.Add(numshaderz, "ynum",
            //                            GLRenderableItem.CreateVector4Vector2Matrix4(items, rq,
            //                                    GLShapeObjectFactory.CreateQuad(500.0f), GLShapeObjectFactory.TexQuad, numberposz,
            //                                    rt, numberposz.Length));
            //}




        }

        private void SystemTick(object sender, EventArgs e)
        {
            gl3dcontroller.HandleKeyboardSlews(true, OtherKeys);
            //if (cdmt.AnythingChanged)
            //    gl3dcontroller.Redraw();
        }

        private void OtherKeys(BaseUtils.KeyboardState kb)
        {
            if (kb.IsPressedRemove(Keys.F1, BaseUtils.KeyboardState.ShiftState.None))
            {
                int times = 1000;
                System.Diagnostics.Debug.WriteLine("Start test");
                long tickcount = gl3dcontroller.Redraw(times);
                System.Diagnostics.Debug.WriteLine("Redraw {0} ms per {1}", tickcount, (float)tickcount / (float)times);
            }
        }


    }
}


