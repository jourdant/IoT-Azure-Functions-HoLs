using Microsoft.ProjectOxford.Vision.Contract;
using Microsoft.ProjectOxford.Vision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IgniteIoT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Brush for drawing the bounding box around each identified face.
        /// </summary>
        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Orange);

        /// <summary>
        /// Thickness of the face bounding box lines.
        /// </summary>
        private readonly double lineThickness = 2.0;

        /// <summary>
        /// Transparent fill for the bounding box.
        /// </summary>
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);

        /// <summary>
        /// Holds the current scenario state value.
        /// </summary>
        private State currentState;

        /// <summary>
        /// References a MediaCapture instance; is null when not in Streaming state.
        /// </summary>
        private MediaCapture mediaCapture;

        /// <summary>
        /// Cache of properties from the current MediaCapture device which is used for capturing the preview frame.
        /// </summary>
        private VideoEncodingProperties videoProperties;

        /// <summary>
        /// References a FaceTracker instance.
        /// </summary>
        private FaceTracker faceTracker;

        /// <summary>
        /// A periodic timer to execute FaceTracker on preview frames
        /// </summary>
        private ThreadPoolTimer frameProcessingTimer;

        /// <summary>
        /// Semaphore to ensure FaceTracking logic only executes one at a time
        /// </summary>
        private SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// File to store image
        /// </summary>
        private StorageFile file;



        public MainPage()
        {
            this.InitializeComponent();

            this.currentState = State.Idle;
            App.Current.Suspending += this.OnSuspending;
        }

        /// <summary>
        /// Values for identifying and controlling scenario states.
        /// </summary>
        private enum State
        {
            /// <summary>
            /// Display is blank - default state.
            /// </summary>
            Idle,

            /// <summary>
            /// Webcam is actively engaged and a live video stream is displayed.
            /// </summary>
            Streaming
        }

        /// <summary>
        /// Responds when we navigate to this page.
        /// </summary>
        /// <param name="e">Event data</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // The 'await' operation can only be used from within an async method but class constructors
            // cannot be labeled as async, and so we'll initialize FaceTracker here.
            if (this.faceTracker == null)
            {
                this.faceTracker = await FaceTracker.CreateAsync();
            }
        }

        /// <summary>
        /// Responds to App Suspend event to stop/release MediaCapture object if it's running and return to Idle state.
        /// </summary>
        /// <param name="sender">The source of the Suspending event</param>
        /// <param name="e">Event data</param>
        private void OnSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (this.currentState == State.Streaming)
            {
                var deferral = e.SuspendingOperation.GetDeferral();
                try
                {
                    this.ChangeState(State.Idle);
                }
                finally
                {
                    deferral.Complete();
                }
            }
        }

        /// <summary>
        /// Initializes a new MediaCapture instance and starts the Preview streaming to the CamPreview UI element.
        /// </summary>
        /// <returns>Async Task object returning true if initialization and streaming were successful and false if an exception occurred.</returns>
        private async Task<bool> StartWebcamStreaming()
        {
            bool successful = true;

            try
            {
                this.mediaCapture = new MediaCapture();

                // For this scenario, we only need Video (not microphone) so specify this in the initializer.
                // NOTE: the appxmanifest only declares "webcam" under capabilities and if this is changed to include
                // microphone (default constructor) you must add "microphone" to the manifest or initialization will fail.
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = StreamingCaptureMode.Video;
                await this.mediaCapture.InitializeAsync(settings);
                this.mediaCapture.Failed += this.MediaCapture_CameraStreamFailed;

                // Cache the media properties as we'll need them later.
                var deviceController = this.mediaCapture.VideoDeviceController;
                this.videoProperties = deviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                // Immediately start streaming to our CaptureElement UI.
                // NOTE: CaptureElement's Source must be set before streaming is started.
                this.CamPreview.Source = this.mediaCapture;
                await this.mediaCapture.StartPreviewAsync();

                // Use a 66 millisecond interval for our timer, i.e. 15 frames per second
                TimeSpan timerInterval = TimeSpan.FromMilliseconds(2000);
                this.frameProcessingTimer = Windows.System.Threading.ThreadPoolTimer.CreatePeriodicTimer(new Windows.System.Threading.TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);
            }
            catch (System.UnauthorizedAccessException)
            {
                // If the user has disabled their webcam this exception is thrown; provide a descriptive message to inform the user of this fact.
                this.NotifyUser("Webcam is disabled or access to the webcam is disabled for this app.\nEnsure Privacy Settings allow webcam usage.", NotifyType.ErrorMessage);
                successful = false;
            }
            catch (Exception ex)
            {
                this.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                successful = false;
            }

            return successful;
        }

        /// <summary>
        /// Safely stops webcam streaming (if running) and releases MediaCapture object.
        /// </summary>
        private async void ShutdownWebCam()
        {
            if (this.frameProcessingTimer != null)
            {
                this.frameProcessingTimer.Cancel();
            }

            if (this.mediaCapture != null)
            {
                if (this.mediaCapture.CameraStreamState == Windows.Media.Devices.CameraStreamState.Streaming)
                {
                    try
                    {
                        await this.mediaCapture.StopPreviewAsync();
                    }
                    catch (Exception)
                    {
                        ;   // Since we're going to destroy the MediaCapture object there's nothing to do here
                    }
                }
                this.mediaCapture.Dispose();
            }

            this.frameProcessingTimer = null;
            this.CamPreview.Source = null;
            this.mediaCapture = null;
            this.CameraStreamingButton.IsEnabled = true;

        }

        /// <summary>
        /// This method is invoked by a ThreadPoolTimer to execute the FaceTracker and Visualization logic at approximately 15 frames per second.
        /// </summary>
        /// <remarks>
        /// Keep in mind this method is called from a Timer and not synchronized with the camera stream. Also, the processing time of FaceTracker
        /// will vary depending on the size of each frame and the number of faces being tracked. That is, a large image with several tracked faces may
        /// take longer to process.
        /// </remarks>
        /// <param name="timer">Timer object invoking this call</param>
        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {
            if (this.currentState != State.Streaming)
            {
                return;
            }

            // If a lock is being held it means we're still waiting for processing work on the previous frame to complete.
            // In this situation, don't wait on the semaphore but exit immediately.
            if (!frameProcessingSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                IList<DetectedFace> faces = null;

                // Create a VideoFrame object specifying the pixel format we want our capture image to be (NV12 bitmap in this case).
                // GetPreviewFrame will convert the native webcam frame into this format.
                const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Nv12;
                using (VideoFrame previewFrame = new VideoFrame(InputPixelFormat, (int)this.videoProperties.Width, (int)this.videoProperties.Height))
                {
                    await mediaCapture.GetPreviewFrameAsync(previewFrame);

                    // The returned VideoFrame should be in the supported NV12 format but we need to verify this.
                    if (FaceDetector.IsBitmapPixelFormatSupported(previewFrame.SoftwareBitmap.BitmapPixelFormat))
                    {
                        faces = await this.faceTracker.ProcessNextFrameAsync(previewFrame);

                        if (faces != null)
                        {
                            ImageEncodingProperties imgFormat = ImageEncodingProperties.CreateJpeg();

                            // create storage file in local app storage
                            file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                                "TestPhoto.jpg", CreationCollisionOption.GenerateUniqueName);

                            // take photo
                            await mediaCapture.CapturePhotoToStorageFileAsync(imgFormat, file);

                        }

                    }
                    else
                    {
                        throw new System.NotSupportedException("PixelFormat '" + InputPixelFormat.ToString() + "' is not supported by FaceDetector");
                    }

                    // Create our visualization using the frame dimensions and face results but run it on the UI thread.
                    var previewFrameSize = new Windows.Foundation.Size(previewFrame.SoftwareBitmap.PixelWidth, previewFrame.SoftwareBitmap.PixelHeight);
                    var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        this.SetupUI(previewFrameSize, faces);
                    });
                }
            }
            catch (Exception ex)
            {
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    this.NotifyUser(ex.ToString(), NotifyType.ErrorMessage);
                });
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }

        }

        private Stream stream = new MemoryStream();
        private CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>
        /// Takes the webcam image and FaceTracker results and assembles the visualization onto the Canvas.
        /// </summary>
        /// <param name="framePizelSize">Width and height (in pixels) of the video capture frame</param>
        /// <param name="foundFaces">List of detected faces; output from FaceTracker</param>
        private async void SetupUI(Windows.Foundation.Size framePizelSize, IList<DetectedFace> foundFaces)
        {

            if (this.currentState == State.Streaming && foundFaces != null)
            {
                foreach (DetectedFace face in foundFaces)
                {
                    // Get photo as a BitmapImage
                    BitmapImage bmpImage = new BitmapImage(new Uri(file.Path));
                    imagePreview.Source = bmpImage;

                    var imageAnalysisResult = await UploadAndAnalyzeImage(file.Path);
                    Debug.WriteLine("Recognizing...");
                    status.Text = "Recognizing...";
                    textBlock.Text = imageAnalysisResult.Description.Captions[0].Text;
                }


            }
        }

        /// <summary>
        /// Get information from cognitive services
        /// </summary>
        /// 

        //private Stream stream = new MemoryStream();

        private async Task<AnalysisResult> UploadAndAnalyzeImage(string imageFilePath)

        {

            //

            // Create Project Oxford Vision API Service client

            //

            try
            {

                VisionServiceClient VisionServiceClient = new VisionServiceClient("5cd43034665d42bb912d04a946ac6512");

                Debug.WriteLine("VisionServiceClient is created");
                status.Text = "VisionServiceClient is created";


                StorageFile file = await StorageFile.GetFileFromPathAsync(imageFilePath);

                using (Stream imageFileStream = (await file.OpenReadAsync()).AsStreamForRead())

                {

                    // Analyze the image for all visual features

                    Debug.WriteLine("Calling VisionServiceClient.AnalyzeImageAsync()...");

                    status.Text = "Calling VisionServiceClient.AnalyzeImageAsync()...";

                    VisualFeature[] visualFeatures = new VisualFeature[]

                    {
                        VisualFeature.Adult, VisualFeature.Categories, VisualFeature.Color, VisualFeature.Description,

                        VisualFeature.Faces, VisualFeature.ImageType, VisualFeature.Tags
                    };


                    AnalysisResult analysisResult =

                        await VisionServiceClient.AnalyzeImageAsync(imageFileStream, visualFeatures);

                    Debug.WriteLine(analysisResult);
                    status.Text = "Image analyzed!";
                    return analysisResult;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }

        }

        /// <summary>
        /// Manages the scenario's internal state. Invokes the internal methods and updates the UI according to the
        /// passed in state value. Handles failures and resets the state if necessary.
        /// </summary>
        /// <param name="newState">State to switch to</param>
        private async void ChangeState(State newState)
        {
            // Disable UI while state change is in progress
            this.CameraStreamingButton.IsEnabled = false;

            switch (newState)
            {
                case State.Idle:

                    this.ShutdownWebCam();

                    this.CameraStreamingButton.Content = "\xE768";
                    this.currentState = newState;
                    break;

                case State.Streaming:

                    if (!await this.StartWebcamStreaming())
                    {
                        this.ChangeState(State.Idle);
                        break;
                    }

                    this.CameraStreamingButton.Content = "\xE71A";
                    this.currentState = newState;
                    this.CameraStreamingButton.IsEnabled = true;
                    break;
            }
        }

        /// <summary>
        /// Handles MediaCapture stream failures by shutting down streaming and returning to Idle state.
        /// </summary>
        /// <param name="sender">The source of the event, i.e. our MediaCapture object</param>
        /// <param name="args">Event data</param>
        private void MediaCapture_CameraStreamFailed(MediaCapture sender, object args)
        {
            // MediaCapture is not Agile and so we cannot invoke its methods on this caller's thread
            // and instead need to schedule the state change on the UI thread.
            var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ChangeState(State.Idle);
            });
        }

        /// <summary>
        /// Handles "streaming" button clicks to start/stop webcam streaming.
        /// </summary>
        /// <param name="sender">Button user clicked</param>
        /// <param name="e">Event data</param>
        private void CameraStreamingButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.currentState == State.Streaming)
            {
                this.NotifyUser(string.Empty, NotifyType.StatusMessage);
                this.ChangeState(State.Idle);
            }
            else
            {
                this.NotifyUser(string.Empty, NotifyType.StatusMessage);
                this.ChangeState(State.Streaming);
            }
        }

        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };

        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }

    }
}
