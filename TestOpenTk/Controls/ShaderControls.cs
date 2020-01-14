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
using OpenTKUtils.GL4.Controls;

// Demonstrate the volumetric calculations needed to compute a plane facing the user inside a bounding box done inside a geo shader
// this one add on tex coord calculation and using a single tight quad shows its working

namespace TestOpenTk
{
    public partial class ShaderControls : Form
    {
        private OpenTKUtils.WinForm.GLWinFormControl glwfc;
        private Controller3D gl3dcontroller;

        private Timer systemtimer = new Timer();

        public ShaderControls()
        {
            InitializeComponent();

            glwfc = new OpenTKUtils.WinForm.GLWinFormControl(glControlContainer);

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();
        Vector4[] boundingbox;
        GLForm form;

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////


        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
        }

        public class GLFixedShader : GLShaderPipeline
        {
            public GLFixedShader(Color c, Action<IGLProgramShader> action = null) : base(action)
            {
                AddVertexFragment(new GLPLVertexShaderWorldCoord(), new GLPLFragmentShaderFixedColour(c));
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            items.Add("MCUB", new GLMatrixCalcUniformBlock());     // create a matrix uniform block 

            int front = -20000, back = front + 90000, left = -45000, right = left + 90000, vsize = 2000;
            boundingbox = new Vector4[]
            {
                new Vector4(left,-vsize,front,1),
                new Vector4(left,vsize,front,1),
                new Vector4(right,vsize,front,1),
                new Vector4(right,-vsize,front,1),

                new Vector4(left,-vsize,back,1),
                new Vector4(left,vsize,back,1),
                new Vector4(right,vsize,back,1),
                new Vector4(right,-vsize,back,1),
            };

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
                items.Add("LINEYELLOW", new GLFixedShader(System.Drawing.Color.Yellow, (a) => { GLStatics.LineWidth(1); }));
                rObjects.Add(items.Shader("LINEYELLOW"),
                GLRenderableItem.CreateVector4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Lines, displaylines));
            }

            float h = 0;
            if ( h != -1)
            {
                items.Add("COS-1L", new GLColourShaderWithWorldCoord((a) => { GLStatics.LineWidth(1); }));

                int dist = 1000;
                Color cr = Color.FromArgb(100, Color.White);
                rObjects.Add(items.Shader("COS-1L"),    // horizontal
                             GLRenderableItem.CreateVector4Color4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Lines,
                                                        GLShapeObjectFactory.CreateLines(new Vector3(left, h, front), new Vector3(left, h, back), new Vector3(dist, 0, 0), (back - front) / dist + 1),
                                                        new Color4[] { cr })
                                   );

                rObjects.Add(items.Shader("COS-1L"),
                             GLRenderableItem.CreateVector4Color4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Lines,
                                                        GLShapeObjectFactory.CreateLines(new Vector3(left, h, front), new Vector3(right, h, front), new Vector3(0, 0, dist), (right - left) / dist + 1),
                                                        new Color4[] { cr })
                                   );

            }

            {
                items.Add("TEX", new GLTexturedShaderWithObjectTranslation());
                items.Add("dotted2", new GLTexture2D(Properties.Resources.dotted2));

                rObjects.Add(items.Shader("TEX"),
                    GLRenderableItem.CreateVector4Vector2(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles,
                            GLCubeObjectFactory.CreateSolidCubeFromTriangles(2000f), GLCubeObjectFactory.CreateCubeTexTriangles(),
                            new GLObjectDataTranslationRotationTexture(items.Tex("dotted2"), new Vector3(-2, 0, 0))
                            ));
            }

            {
                items.Add("FCS1", new GLFixedColourShaderWithWorldCoord(Color.FromArgb(150, Color.Green)));
                items.Add("FCS2", new GLFixedColourShaderWithWorldCoord(Color.FromArgb(80, Color.Red)));

                rObjects.Add(items.Shader("FCS1"),
                    GLRenderableItem.CreateVector4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Quads,
                                                GLShapeObjectFactory.CreateQuad(1000, pos: new Vector3(4000, 500, 0))));
                rObjects.Add(items.Shader("FCS2"),
                    GLRenderableItem.CreateVector4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Quads,
                                                GLShapeObjectFactory.CreateQuad(1000, pos: new Vector3(4000, 1000, 0))));
            }

            if (true)
            {
                form = new GLForm(glwfc);       // hook form to the window - its the master
                form.Focusable = true;          // we want to be able to focus and receive key presses.
                form.Name = "form";
                form.SuspendLayout();

                GLPanel ptop = new GLPanel("paneltop", new Rectangle(10, 0, 1000, 800), Color.FromArgb(200, Color.Red));
                ptop.BackColorGradient = 90;
                ptop.BackColorGradientAlt = Color.Yellow;

                //GLPanel ptop = new GLPanel("paneltop", new Rectangle(10, 0, 1000, 800), Color.Transparent);
                ptop.SetMarginBorderWidth(new Margin(2), 1, Color.Wheat, new OpenTKUtils.GL4.Controls.Padding(2));
                form.Add(ptop);

                if (false)
                {
                    GLTableLayoutPanel ptable = new GLTableLayoutPanel("paneltop", new Rectangle(150, 10, 200, 200), Color.Gray);
                    ptable.SuspendLayout();
                    ptable.Rows = new List<GLTableLayoutPanel.Style> { new GLTableLayoutPanel.Style(GLTableLayoutPanel.Style.SizeTypeEnum.Relative, 50), new GLTableLayoutPanel.Style(GLTableLayoutPanel.Style.SizeTypeEnum.Relative, 50) };
                    ptable.Columns = new List<GLTableLayoutPanel.Style> { new GLTableLayoutPanel.Style(GLTableLayoutPanel.Style.SizeTypeEnum.Relative, 50), new GLTableLayoutPanel.Style(GLTableLayoutPanel.Style.SizeTypeEnum.Relative, 50) };
                    ptop.Add(ptable);
                    GLImage pti1 = new GLImage("PTI1", new Rectangle(0, 0, 24, 24), Properties.Resources.dotted);
                    pti1.Column = 0; pti1.Row = 0; pti1.Dock = DockingType.Fill;
                    ptable.Add(pti1);
                    GLImage pti2 = new GLImage("PTI2", new Rectangle(100, 0, 24, 24), Properties.Resources.dotted2);
                    pti2.Column = 1; pti1.Row = 0;
                    ptable.Add(pti2);
                    GLImage pti3 = new GLImage("PTI3", new Rectangle(100, 0, 48, 48), Properties.Resources.ImportSphere);
                    pti3.Column = 0; pti3.Row = 1; pti3.Dock = DockingType.LeftCenter; pti3.ImageStretch = true;
                    ptable.Add(pti3);
                    GLImage pti4 = new GLImage("PTI4", new Rectangle(100, 0, 64, 64), Properties.Resources.Logo8bpp);
                    pti4.Column = 1; pti4.Row = 1; pti4.Dock = DockingType.Center;
                    ptable.Add(pti4);
                    ptable.ResumeLayout();
                }

                if (false)
                {
                    GLTextBox tb1 = new GLTextBox("TB1", new Rectangle(600, 10, 150, 20), "Text Data Which is a very long string", Color.White);
                    ptop.Add(tb1);
                }

                if (false)
                {
                    GLPanel psb = new GLPanel("panelsb", new Rectangle(600, 50, 50, 220), Color.Gray);
                    ptop.Add(psb);
                    GLScrollBar sb1 = new GLScrollBar("SB1", new Rectangle(5, 10, 40, 200), 0, 100);
                    psb.Add(sb1);
                }

                if (false)
                {
                    GLVerticalScrollPanel sp1 = new GLVerticalScrollPanel("SP1", new Rectangle(150, 220, 200, 200), Color.Gray);
                    ptop.Add(sp1);
                    GLImage sp1i1 = new GLImage("SP1I1", new Rectangle(10, 10, 100, 100), Properties.Resources.dotted);
                    sp1.Add(sp1i1);
                    GLImage sp1i2 = new GLImage("SP1I2", new Rectangle(10, 120, 100, 100), Properties.Resources.dotted);
                    sp1.Add(sp1i2);
                }

                if (false)
                {
                    GLVerticalScrollPanelScrollBar spb1 = new GLVerticalScrollPanelScrollBar("CSPan", new Rectangle(370, 220, 200, 200), Color.Gray);
                    ptop.Add(spb1);
                    GLImage spb1i1 = new GLImage("SPB1I1", new Rectangle(10, 10, 100, 100), Properties.Resources.dotted);
                    spb1.Add(spb1i1);
                    GLImage spb1i2 = new GLImage("SPB1I2", new Rectangle(10, 120, 100, 100), Properties.Resources.dotted);
                    spb1.Add(spb1i2);
                }

                if (true)
                {
                    List<string> i1 = new List<string>() { "one", "two", "three", "four" ,"five","six","seven","eight","nine","ten","eleven","twelve"};
                    GLListBox lb1 = new GLListBox("LB1", new Rectangle(580, 220, 200, 200), i1, Color.Gray);
                    lb1.Font = new Font("Microsoft Sans Serif", 12f);
                    ptop.Add(lb1);
                    lb1.SelectedIndexChanged += (s,si) => { System.Diagnostics.Debug.WriteLine("Selected index " + si); };
                }

                if (true)
                {
                    GLPanel p2 = new GLPanel("P2", DockingType.Left, 0.15f, Color.Green);
                    ptop.Add(p2);

                    GLPanel p3 = new GLPanel("P3", DockingType.Right, 0.1f, Color.Yellow);
                    ptop.Add(p3);

                    GLButton b1 = new GLButton("B1", new Rectangle(5, 5, 0, 0), "Button 1", Color.Gray);
                    b1.Click += (c, ev) => { System.Diagnostics.Debug.WriteLine("On click for " + c.Name + " " + ev.Button); };
                    p2.Add(b1);

                    GLButton b2 = new GLButton("B2", new Rectangle(5, 30, 0, 0), "Button 2", Color.Gray);
                    b2.Image = Properties.Resources.ImportSphere;
                    b2.ImageAlign = ContentAlignment.MiddleLeft;
                    b2.TextAlign = ContentAlignment.MiddleRight;
                    p2.Add(b2);

                    GLCheckBox cb1 = new GLCheckBox("CB1", new Rectangle(5, 70, 100, 20), "Check Box 1", Color.Transparent);
                    cb1.AutoCheck = true;
                    cb1.CheckChanged += (c, ev) => { System.Diagnostics.Debug.WriteLine("Check changed " + c.Name + " " + ev.Button); };
                    p2.Add(cb1);

                    GLLabel lb1 = new GLLabel("Lab1", new Rectangle(5, 100, 0,0), "Hello", Color.Red);
                    p2.Add(lb1);
                }

                if (false)
                {
                    GLPanel ptop2 = new GLPanel();
                    ptop2.Position = new Rectangle(1012, 400, 400, 400);
                    ptop2.BackColor = Color.Blue;
                    ptop2.Name = "paneltop2";
                    form.Add(ptop2);

                    GLImage i1 = new GLImage("I1", new Rectangle(10, 120, 200, 200), Properties.Resources.dotted);
                    ptop2.Add(i1);
                }

                form.ResumeLayout();
            }

            gl3dcontroller = new Controller3D();    

            gl3dcontroller.MatrixCalc.PerspectiveNearZDistance = 1f;
            gl3dcontroller.MatrixCalc.PerspectiveFarZDistance = 500000f;
            gl3dcontroller.ZoomDistance = 5000F;
            gl3dcontroller.EliteMovement = true;
            gl3dcontroller.PaintObjects = Controller3dDraw;

            gl3dcontroller.KeyboardTravelSpeed = (ms) =>
            {
                return (float)ms * 10.0f;
            };

            gl3dcontroller.MatrixCalc.InPerspectiveMode = true;

            if ( form != null )
            {
                gl3dcontroller.Start(form, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F);     // HOOK the 3dcontroller to the form so it gets Form events

                form.Paint += (o) =>        // subscribing after start means we paint over the scene, letting transparency work
                {                           // this is because we are at depth 0
                    GLMatrixCalc c = new GLMatrixCalc();
                    ((GLMatrixCalcUniformBlock)items.UB("MCUB")).Set(c,glwfc.Width, glwfc.Height);        // set the matrix unform block to the controller 3d matrix calc.
                    form.Render();
                };

            }
            else
                gl3dcontroller.Start(glwfc, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F);     // HOOK the 3dcontroller to the form so it gets Form events

        }


        private void Controller3dDraw(GLMatrixCalc mc, long time)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).Set(gl3dcontroller.MatrixCalc, glwfc.Width, glwfc.Height);        // set the matrix unform block to the controller 3d matrix calc.

            rObjects.Render(gl3dcontroller.MatrixCalc);

            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.TargetPosition + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " dir " + gl3dcontroller.Camera.Current + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance + " Zoom " + gl3dcontroller.Zoom.Current;
        }

        private void SystemTick(object sender, EventArgs e)
        {
            if (form != null && form.RequestRender)
                glwfc.Invalidate();
            var cdmt = gl3dcontroller.HandleKeyboard(true);
            if (cdmt.AnythingChanged )
                glwfc.Invalidate();
        }

    }
}


