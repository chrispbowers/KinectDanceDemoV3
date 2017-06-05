using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Kinect;
using OpenTK;
using Microsoft.Kinect.Face;
using Gma.System.MouseKeyHook;
using System.Windows.Input;

namespace KinectTestv2
{
    public partial class FormMDImain : Form
    {



        //maximumFrameGap between face frames (low numbers effect stabilty and framerate of cloud point render - high numbers decrease framerate of face detection)
        int maxFaceFrameGap = 5;

        //how many face frames should the face propery exists before we act
        int eyeWindowSize = 2;

        //frame sizes
        static int depthWidth = 512;
        static int depthHeight = 424;
        static int bodyWidth = 512;
        static int bodyHeight = 424;
        static int colorWidth = 1920;
        static int colorHeight = 1080;

        static int EyesOpen = 0;
        static int EyesWink = 1;
        static int EyesClosed = 2;

        bool colorFrameProcessed = false;
        bool depthFrameProcessed = false;
        bool bodyFrameProcessed = false;
        bool bodyIndexFrameProcessed = false;
        int faceFrameGapCounter = 0;


        //windows form components
        RGB_3Dform1 processedCloud;
        ColourForm colourForm;

        //demo states
        int bodyCount;
        int indexBodyCount = 0;
        bool fadeEnabled = false;
        bool forceFadeEnabled = false;
        bool displayRaw = true;
        bool bottomEffect = true;
        static float fadeOutRate = 0.1f;
        static int fadeHeldLength = 25;
        static float fadeInRate = 0.1f;
        
        bool bDestructFade = false;

       
        //user face expression states
        int[] eyesClosedCount;
        int[] eyes;
        bool[] fadeStarted;
        float[] fadeState;
        int[] fadeIndex;

        // Store kinect frames and data
        ColorSpacePoint[] colorPoints = new ColorSpacePoint[depthWidth * depthHeight];    // Maps depth pixels to rgb pixels
        CameraSpacePoint[] cameraPoints = new CameraSpacePoint[depthWidth * depthHeight];     // Maps depth pixels to 3d coordinates
        CoordinateMapper _mapper;
        FaceFrameSource[] faceFrameSources;
        FaceFrameReader[] faceFrameReaders;
        FaceFrameResult[] faceFrameResults;
        FaceFrameResult[] prevFaceFrameResults;
        ushort[] depthFrameData = new ushort[depthWidth * depthHeight];
        byte[] colourFrameData = new byte[colorWidth * colorHeight * 4];
        byte[] bodyIndexFrameData = new byte[bodyWidth * bodyHeight];


        Dictionary<Vector3, OpenTK.Vector4> RGBpointCloud = new Dictionary<Vector3, OpenTK.Vector4>(); 

        //store array of tracked bodies
        Body[] bodies = new Body[6];

        KinectSensor _sensor;



        public FormMDImain()
        {
            InitializeComponent();


            //get instance of sensor - only one sensor allowed
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {

                _mapper = _sensor.CoordinateMapper;

                MultiSourceFrameReader multiSourceFrameReader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);

                bodyCount = _sensor.BodyFrameSource.BodyCount;

                //user face expression states counter
                eyesClosedCount = new int[bodyCount];
                eyes = new int[bodyCount];
                fadeStarted = new bool[bodyCount];
                fadeState = new float[] {1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f};
                fadeIndex = new int[bodyCount];


                FaceFrameFeatures faceFeatures = FaceFrameFeatures.BoundingBoxInColorSpace |
                                                 FaceFrameFeatures.FaceEngagement |
                                                 FaceFrameFeatures.Glasses |
                                                 FaceFrameFeatures.Happy |
                                                 FaceFrameFeatures.LeftEyeClosed |
                                                 FaceFrameFeatures.MouthOpen |
                                                 FaceFrameFeatures.PointsInColorSpace |
                                                 FaceFrameFeatures.RightEyeClosed;

                faceFrameSources = new FaceFrameSource[bodyCount];
                faceFrameReaders = new FaceFrameReader[bodyCount];
                faceFrameResults = new FaceFrameResult[bodyCount];
                prevFaceFrameResults = new FaceFrameResult[bodyCount];

                for (int i = 0; i < bodyCount; i++)
                {
                    faceFrameSources[i] = new FaceFrameSource(_sensor, 0, faceFeatures);
                    faceFrameReaders[i] = faceFrameSources[i].OpenReader();
                }

                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameArrived;

                _sensor.Open();

            }

            processedCloud = new RGB_3Dform1();
            colourForm = new ColourForm(_mapper);

            //processedCloud.MdiParent = this;
            //colourForm.MdiParent = this;

            processedCloud.Show();
            colourForm.Show();

        }


        private void FormMDImain_Load(object sender, EventArgs e)
        {

        }

   

        private void MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("MultiSourceFrameArrived - " + faceFrameGapCounter + " - " + System.DateTime.Now);

            //if paused then don't handle main loop
            if (Keyboard.GetKeyStates(Key.P) == KeyStates.Toggled)
            {
                System.Diagnostics.Debug.WriteLine("PAUSED");
                return;
            }

            if ((Keyboard.GetKeyStates(Key.R) == KeyStates.Toggled) == displayRaw)
            {
                displayRaw = !displayRaw;
                if(displayRaw) System.Diagnostics.Debug.WriteLine("Raw ON");
                else System.Diagnostics.Debug.WriteLine("Raw OFF");

            }

            if ((Keyboard.GetKeyStates(Key.B) == KeyStates.Toggled) == bottomEffect)
            {
                bottomEffect = !bottomEffect;
                if (bottomEffect) System.Diagnostics.Debug.WriteLine("Shadow ON");
                else System.Diagnostics.Debug.WriteLine("Shadow OFF");

            }

           


            if ((Keyboard.GetKeyStates(Key.F) == KeyStates.Toggled) != fadeEnabled)
            {
                fadeEnabled = !fadeEnabled;
                if (fadeEnabled) System.Diagnostics.Debug.WriteLine("Fade ON");
                else System.Diagnostics.Debug.WriteLine("Fade OFF");

            }

            if ((Keyboard.GetKeyStates(Key.S) == KeyStates.Toggled) != processedCloud.cameraSweepMode)
            {
                processedCloud.cameraSweepMode = !processedCloud.cameraSweepMode;
                if (processedCloud.cameraSweepMode) System.Diagnostics.Debug.WriteLine("Camera Sweep ON");
                else System.Diagnostics.Debug.WriteLine("Camera Sweep OFF");

            }
            if ((Keyboard.GetKeyStates(Key.Z) == KeyStates.Toggled) != forceFadeEnabled)
            {
                forceFadeEnabled = !forceFadeEnabled;
                System.Diagnostics.Debug.WriteLine("Force Fade");

               fadeStarted = new bool[] { true, true, true, true, true, true };
               
            }


           

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            colorFrameProcessed = false;
            depthFrameProcessed = false;
            bodyFrameProcessed = false;
            bodyIndexFrameProcessed = false;


            if (multiSourceFrame == null) return;

            using (BodyFrame bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    indexBodyCount = 0;
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    // iterate through each face source
                    for (int i = 0; i < this.bodyCount; i++)
                    {
                        if (bodies[i].IsTracked)
                        {
                            indexBodyCount++;
                            // check if a valid face is tracked in this face source
                            if (!faceFrameSources[i].IsTrackingIdValid)
                            {
                                // update the face frame source to track this body
                                faceFrameSources[i].TrackingId = bodies[i].TrackingId;
                            }
                        }
                    }
                    bodyFrameProcessed = true;
                    
                }


            }

            using (DepthFrame depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    depthFrameData = new ushort[depthWidth * depthHeight];
                    depthFrame.CopyFrameDataToArray(depthFrameData);
                    depthFrameProcessed = true;
                }
            }

            using (ColorFrame colourFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            {
                if (colourFrame != null)
                {
                    colourFrame.CopyConvertedFrameDataToArray(colourFrameData, ColorImageFormat.Bgra);
                    colorFrameProcessed = true;
                }
            }


            using (BodyIndexFrame bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame())
            {
                if (bodyIndexFrame != null)
                {
                    //bodyIndexFrameData = new byte[bodyWidth * bodyHeight];
                    bodyIndexFrame.CopyFrameDataToArray(bodyIndexFrameData);
                    bodyIndexFrameProcessed = true;
                }
            }

            for(int i=0; i<bodyCount; i++)
            {
                using (FaceFrame faceFrame = faceFrameReaders[i].AcquireLatestFrame()) {

                    if (faceFrame == null) break;

                    faceFrameGapCounter = 0;

                    //copy old faceframe result to prev array
                    prevFaceFrameResults[i] = faceFrameResults[i];

                    // store this face frame result to use later
                    faceFrameResults[i] = faceFrame.FaceFrameResult;
                    
                }
            }
           

            // update draw states if new data
            if (bodyFrameProcessed) colourForm._bodies = bodies;
            if (colorFrameProcessed) colourForm.updatePicture(colourFrameData);
            if (faceFrameGapCounter == 0) faceState();
            if (depthFrameProcessed && colorFrameProcessed && bodyIndexFrameProcessed && faceFrameGapCounter < maxFaceFrameGap) CreateVertices();

            //iterate facegap counter to maintain count of number of frames since last processing a face
            faceFrameGapCounter++;

        }


        public void faceState()
        {
            //System.Diagnostics.Debug.WriteLine("faceState - " + System.DateTime.Now);
   
            int trackedBodyCount = 0;


            for (int i = 0; i < bodyCount; i++)
            {
                if (prevFaceFrameResults[i] != null && faceFrameResults[i] != null)
                {

                    //eyes closed  or wink
                    if (faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed]
                        &&
                        faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed])
                    {
                        eyesClosedCount[i]++;
                        //System.Diagnostics.Debug.WriteLine(eyesClosedCount[i]);
                    }
                    else
                    {
                        eyesClosedCount[i] = 0;
                        //System.Diagnostics.Debug.WriteLine("State change");
                    }
                    if (eyesClosedCount[i] >= eyeWindowSize)
                    {
                        eyesClosedCount[i] = 0;
                        //System.Diagnostics.Debug.WriteLine("Stable state");
                        if (faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == DetectionResult.Yes
                            &&
                            faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == DetectionResult.Yes) eyes[i] = EyesClosed;
                        else eyes[i] = EyesOpen;

                        //System.Diagnostics.Debug.WriteLine("New state is " + eyes[i]);

                    }


                }

                if (eyes[i] == EyesClosed && !fadeStarted[i] && fadeEnabled) {
                    fadeStarted[i] = true;
                    System.Diagnostics.Debug.Write("User " + i + " Closed eyes");
                }


                if (fadeStarted[i])
                {
                    System.Diagnostics.Debug.WriteLine("Fade state " + i + ", " + fadeState[i]);
                    if (fadeIndex[i] == 0 && fadeState[i] > 0.0f) fadeState[i] = Math.Max(0.0f, fadeState[i] -  fadeOutRate);  //fade in
                    else if (fadeState[i] == 1.0f && fadeIndex[i] >= fadeHeldLength)
                    {
                        fadeIndex[i] = 0;
                        fadeStarted[i] = false;
                        bDestructFade = true;

                        //System.Diagnostics.Debug.WriteLine("Fade Enabled " + index + ", " + fadeEnabled);
                    } //fade reset

                    else if (fadeIndex[i] >= fadeHeldLength) fadeState[i] = Math.Min(1.0f, fadeState[i] + fadeInRate);     //fade out

                    else fadeIndex[i]++; //fade hold

                }



                if (bodies[i] != null && bodies[i].IsTracked)
                {
                        //System.Diagnostics.Debug.Write("UserID=" + i + " eyes = " + eyes[i]);
                         //System.Diagnostics.Debug.WriteLine("");
                }




               
            }
                 
            



        }


        //Creates vertices from frames for rendering to openGL
        public void CreateVertices()
        {

            //System.Diagnostics.Debug.WriteLine("Fade Enabled " + fadeEnabled);

           

            int highestPersonFound = -1;

            RGBpointCloud = new Dictionary<Vector3, OpenTK.Vector4>();


            _mapper.MapDepthFrameToCameraSpace(depthFrameData, cameraPoints);
            _mapper.MapDepthFrameToColorSpace(depthFrameData, colorPoints);


            for (int i = 0; i < cameraPoints.Length; i++)
            {

                //check colour point is within the colour bounds
                ColorSpacePoint p = colorPoints[i];
                if (p.X < 0 || p.Y < 0 || p.X > colorWidth || p.Y > colorHeight) continue;

                int idx = (int)p.X + colorWidth * (int)p.Y;



                //if filtering data account for following exceptions
                if (!displayRaw)
                {

                    int index = bodyIndexFrameData[i];




                    //if background index (ie not allocated to a tracked body
                    if (index == 0xff) continue;

                    if (highestPersonFound < 0) highestPersonFound = index;

                    //dont display people with their eyes shut unless we are fading them
                    if (eyes[index] == EyesClosed && !fadeEnabled) continue;



                    //the bottom effect
                    if (indexBodyCount >= 2)
                    {
                        if (highestPersonFound == index)
                        {
                            if (bottomEffect)
                            {
                                RGBpointCloud.Add(
                                    new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                                    new OpenTK.Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                                continue;
                            }

                        }
                    }

                    RGBpointCloud.Add(
                       new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                       new OpenTK.Vector4(colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 0] / 255f, fadeState[index]));

                }

                else
                {

                    //default case - populate  vertices with RGB colour 
                    RGBpointCloud.Add(
                        new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                        new OpenTK.Vector4(colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 0] / 255f, 1.0f));
                }
           }

            processedCloud.updateVertices(RGBpointCloud.Values.ToArray<OpenTK.Vector4>(), RGBpointCloud.Keys.ToArray<Vector3>());
            
        }

        
        
    }
}
