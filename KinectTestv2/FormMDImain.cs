using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Kinect;
using OpenTK;
using Microsoft.Kinect.Face;
using Gma.System.MouseKeyHook;

namespace KinectTestv2
{
    public partial class FormMDImain : Form
    {



        //maximumFrameGap between face frames (low numbers effect stabilty and framerate of cloud point render - high numbers decrease framerate of face detection)
        int maxFaceFrameGap = 5;

        //how many face frames should the face propery exists before we act
        int mouthWindowSize = 1;
        int eyeWindowSize = 4;
        int engagedWindowSize = 1;
        int expressionWindowSize = 1;
        int glassesWindowSize = 1;

        //frame sizes
        static int depthWidth = 512;
        static int depthHeight = 424;
        static int bodyWidth = 512;
        static int bodyHeight = 424;
        static int colorWidth = 1920;
        static int colorHeight = 1080;


        bool colorFrameProcessed = false;
        bool depthFrameProcessed = false;
        bool bodyFrameProcessed = false;
        bool bodyIndexFrameProcessed = false;
        int faceFrameGapCounter = 0;


        Bitmap outputImage = null;

        //windows form components
        RGB_3Dform1 processedCloud;
        ColourForm colourForm;

        //demo states
        Graphics graphics;
        int firstSeenbodyIndex = -1;
        int bodyCount;
        bool displayRaw = false;
        bool allBodiesLookingAtCamera = true;

        //user face expression states
        int[] mouthCount;
        int[] eyeRightCount;
        int[] eyeLeftCount;
        int[] engagedCount;
        int[] expressionCount;
        int[] glassesCount;

        DetectionResult[] mouth;
        DetectionResult[] eyeRight;
        DetectionResult[] eyeLeft;
        DetectionResult[] engaged;
        DetectionResult[] expression;
        DetectionResult[] glasses;

       

        private IKeyboardMouseEvents m_GlobalHook;


        // Store kinekt frames and data
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

            //setup key hooks
            m_GlobalHook = Hook.GlobalEvents();
            m_GlobalHook.KeyPress += KeyPress;

            //get instance of sensor - only one sensor allowed
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {

                _mapper = _sensor.CoordinateMapper;

                MultiSourceFrameReader multiSourceFrameReader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);

                bodyCount = _sensor.BodyFrameSource.BodyCount;

                //user face expression states counter
                mouthCount = new int[bodyCount];
                eyeRightCount = new int[bodyCount];
                eyeLeftCount = new int[bodyCount];
                engagedCount = new int[bodyCount];
                expressionCount = new int[bodyCount];
                glassesCount = new int[bodyCount];

                mouth = new DetectionResult[bodyCount];
                eyeRight = new DetectionResult[bodyCount];
                eyeLeft = new DetectionResult[bodyCount];
                engaged = new DetectionResult[bodyCount];
                expression = new DetectionResult[bodyCount];
                glasses = new DetectionResult[bodyCount];

               

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

        private void KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'r':
                    displayRaw = !displayRaw;
                    break;


                case 'p':
                    if (_sensor.IsOpen) _sensor.Close();
                    else _sensor.Open();
                    break;

            }
        }


        private void MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("MultiSourceFrameArrived - " + faceFrameGapCounter + " - " + System.DateTime.Now);

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
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    // iterate through each face source
                    for (int i = 0; i < this.bodyCount; i++)
                    {
                        if (bodies[i].IsTracked)
                        {
                         
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

                    // check if this face frame has valid face frame results
                    if (faceFrame.FaceFrameResult != null)
                    {
                    
                        //copy old faceframe result to prev array
                        prevFaceFrameResults[i] = faceFrameResults[i];

                        // store this face frame result to use later
                        faceFrameResults[i] = faceFrame.FaceFrameResult;

                    }

                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        faceFrameResults[i] = null;
                    }
                
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
                    

                    //mouth
                    if (faceFrameResults[i].FaceProperties[FaceProperty.MouthOpen] == prevFaceFrameResults[i].FaceProperties[FaceProperty.MouthOpen]) mouthCount[i]++;
                    else mouthCount[i] = 0;
                    if (mouthCount[i] >= mouthWindowSize) mouth[i] = faceFrameResults[i].FaceProperties[FaceProperty.MouthOpen];
                        
                    //eyes right
                    if (faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed]) eyeRightCount[i]++;
                    else eyeRightCount[i] = 0;
                    if (eyeRightCount[i] >= eyeWindowSize) eyeRight[i] = faceFrameResults[i].FaceProperties[FaceProperty.RightEyeClosed];

                    //eyes left
                    if (faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed] == prevFaceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed]) eyeLeftCount[i]++;
                    else eyeLeftCount[i] = 0;
                    if (eyeLeftCount[i] >= eyeWindowSize) eyeLeft[i] = faceFrameResults[i].FaceProperties[FaceProperty.LeftEyeClosed];

                    //engaged
                    if (faceFrameResults[i].FaceProperties[FaceProperty.Engaged] == prevFaceFrameResults[i].FaceProperties[FaceProperty.Engaged]) engagedCount[i]++;
                    else engagedCount[i] = 0;
                    if (engagedCount[i] > engagedWindowSize) engaged[i] = faceFrameResults[i].FaceProperties[FaceProperty.Engaged];

                    //expression
                    if (faceFrameResults[i].FaceProperties[FaceProperty.Happy] == prevFaceFrameResults[i].FaceProperties[FaceProperty.Happy]) expressionCount[i]++;
                    else expressionCount[i] = 0;
                    if (expressionCount[i] > expressionWindowSize) expression[i] = faceFrameResults[i].FaceProperties[FaceProperty.Happy];

                    //glasses
                    if (faceFrameResults[i].FaceProperties[FaceProperty.WearingGlasses] == prevFaceFrameResults[i].FaceProperties[FaceProperty.WearingGlasses]) glassesCount[i]++;
                    else glassesCount[i] = 0;
                    if (glassesCount[i] > glassesWindowSize) glasses[i] = faceFrameResults[i].FaceProperties[FaceProperty.WearingGlasses];

                  
                    //check to see if all bodies are looking at the camera
                    trackedBodyCount++;
                    if (!(engaged[i] ==DetectionResult.Yes)) allBodiesLookingAtCamera = false;

                }

                if (bodies[i]!=null && bodies[i].IsTracked)
                {
                    System.Diagnostics.Debug.Write("UserID=" + i);
                    if(mouth[i] == DetectionResult.Yes) System.Diagnostics.Debug.Write(", mouthopen");
                    if(eyeRight[i] == DetectionResult.Yes && eyeLeft[i] == DetectionResult.Yes) System.Diagnostics.Debug.Write(", eyesclosed");
                    if(engaged[i] == DetectionResult.Yes) System.Diagnostics.Debug.Write(", engaged");
                    if (expression[i] == DetectionResult.Yes) System.Diagnostics.Debug.Write(", happy");
                    if (glasses[i] == DetectionResult.Yes) System.Diagnostics.Debug.Write(", glasses"); 
                    System.Diagnostics.Debug.WriteLine("");
                }

            }

            if (trackedBodyCount == 0) allBodiesLookingAtCamera = false;

            processedCloud.allBodiesLookingAtCamera = allBodiesLookingAtCamera;


            //if (allBodiesLookingAtCamera) System.Diagnostics.Debug.WriteLine("all looking");


        }


        //Creates vertices from frames for rendering to openGL
        public void CreateVertices()
        {


            RGBpointCloud = new Dictionary<Vector3, OpenTK.Vector4>();


            _mapper.MapDepthFrameToCameraSpace(depthFrameData, cameraPoints);
            _mapper.MapDepthFrameToColorSpace(depthFrameData, colorPoints);


            for (int i = 0; i < cameraPoints.Length; i++)
            {

                //check colour point is within the colour bounds
                ColorSpacePoint p = colorPoints[i];
                if (p.X < 0 || p.Y < 0 || p.X > colorWidth || p.Y > colorHeight) continue;

                //if filtering data account for following exceptions
                if (!displayRaw)
                {
                    //if background index (ie not allocated to a tracked body
                    if (bodyIndexFrameData[i] == 0xff) continue;


                    //dont display people with there eyes shut
                    if (eyeRight[bodyIndexFrameData[i]] == DetectionResult.Yes && eyeLeft[bodyIndexFrameData[i]] == DetectionResult.Yes) continue;
                }

                //populate  vertices
                int idx = (int)p.X + colorWidth * (int)p.Y;
                RGBpointCloud.Add(
                    new Vector3(cameraPoints[i].X, cameraPoints[i].Y, cameraPoints[i].Z),
                    new OpenTK.Vector4(colourFrameData[4 * idx + 2] / 255f, colourFrameData[4 * idx + 1] / 255f, colourFrameData[4 * idx + 0] / 255f, 1.0f));
           }

            processedCloud.updateVertices(RGBpointCloud.Values.ToArray<OpenTK.Vector4>(), RGBpointCloud.Keys.ToArray<Vector3>());
            
        }

        
        
    }
}
