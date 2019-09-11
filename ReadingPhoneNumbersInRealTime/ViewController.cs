using Foundation;
using CoreGraphics;
using System;
using System.Collections.Generic;
using UIKit;
using Vision;
using VisionKit;
using AVFoundation;
using CoreFoundation;
using CoreAnimation;
using CoreMedia;
using CoreVideo;

namespace ReadingPhoneNumbersInRealTime {
	public partial class ViewController : UIViewController, IAVCaptureVideoDataOutputSampleBufferDelegate {
		public CAShapeLayer MaskLayer;
		public UIDeviceOrientation CurrentOrientation;
		AVCaptureSession captureSession;
		public AVCaptureDevice CaptureDevice;
		public AVCaptureVideoDataOutput VideoDataOutput;
		public DispatchQueue VideoDataOutputQueue;
		public CGRect RegionOfInterest;
		public ImageIO.CGImagePropertyOrientation TextOrientation;
		public double BufferAspectRatio;
		public CGAffineTransform UIRotationTransform;
		public CGAffineTransform BottomToTopTransform;
		public CGAffineTransform ROIToGlobalTransform;
		public CGAffineTransform [] VisionToAVFTransform;
		UITapGestureRecognizer gestureRecognizer;

		public ViewController (IntPtr handle) : base (handle)
		{
			MaskLayer = new CAShapeLayer ();
			CurrentOrientation = UIDeviceOrientation.Portrait;
			captureSession = new AVCaptureSession ();
			VideoDataOutput = new AVCaptureVideoDataOutput ();
			VideoDataOutputQueue = new DispatchQueue ("VideoDataOutputQueue");
			RegionOfInterest = new CGRect (0, 0, 1, 1);
			TextOrientation = ImageIO.CGImagePropertyOrientation.Up;
			UIRotationTransform = CGAffineTransform.MakeIdentity ();
			BottomToTopTransform = CGAffineTransform.MakeScale (1, -1);
			BottomToTopTransform.Translate (0, -1);
			ROIToGlobalTransform = CGAffineTransform.MakeIdentity ();
			//VisionToAVFTransform = CGAffineTransform.MakeIdentity ();
			//gestureRecognizer = new UITapGestureRecognizer ((gesture) => {
			//	HandleTap ();
			//});
		}

		public override void ViewDidLoad ()
		{
			Console.WriteLine ("ViewController ViewDidLoad");
			base.ViewDidLoad ();
			Preview.Session = captureSession;
			CutoutView.BackgroundColor = UIColor.Gray.ColorWithAlpha (0.5f);
			MaskLayer.BackgroundColor = UIColor.Clear.CGColor;
			MaskLayer.FillRule = CAShapeLayer.FillRuleEvenOdd;
			CutoutView.Layer.Mask = MaskLayer;
			SetupCamera ();
			CalculateRegionOfInterest ();
			//VisionView.AddGestureRecognizer (gestureRecognizer);
		}

		public override void ViewWillTransitionToSize (CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
		{
			Console.WriteLine ("ViewController ViewWillTransitionToSize");
			base.ViewWillTransitionToSize (toSize, coordinator);
			var deviceOrientation = UIDevice.CurrentDevice.Orientation;
			if (deviceOrientation.IsPortrait () || deviceOrientation.IsLandscape ())
				CurrentOrientation = deviceOrientation;
			var videoPreviewLayerConnection = Preview.VideoPreviewLayer.Connection; // null check here
			videoPreviewLayerConnection.VideoOrientation = (AVCaptureVideoOrientation)deviceOrientation;
			CalculateRegionOfInterest ();
		}

		public override void ViewDidLayoutSubviews ()
		{
			Console.WriteLine ("ViewController ViewDidLayoutSubviews");
			base.ViewDidLayoutSubviews ();
			UpdateCutOut ();
		}

		public override void DidReceiveMemoryWarning ()
		{
			Console.WriteLine ("ViewController DidReceiveMemoryWarning");
			base.DidReceiveMemoryWarning ();
		}

		public void CalculateRegionOfInterest ()
		{
			Console.WriteLine ("CalculateRegionOfInterest");
			var desiredHeightRatio = 0.15;
			var desiredWidthRatio = 0.6;
			var maxPortraitWidth = 0.8;
			CGSize size;
			if (CurrentOrientation.IsPortrait () || CurrentOrientation == UIDeviceOrientation.Unknown)
				size = new CGSize (Math.Min (desiredWidthRatio * BufferAspectRatio, maxPortraitWidth), desiredHeightRatio / BufferAspectRatio);
			else
				size = new CGSize (desiredWidthRatio, desiredHeightRatio);
			Console.WriteLine (CurrentOrientation.IsPortrait ());
			Console.WriteLine (size);
			RegionOfInterest = new CGRect ((1 - size.Width) / 2, (1 - size.Height) / 2, size.Width, size.Height);
			Console.WriteLine (RegionOfInterest);
			SetupOrientationAndTransform ();
			DispatchQueue.MainQueue.DispatchAsync (() => { //make asynch
				UpdateCutOut ();
			});
		}

		public void UpdateCutOut ()
		{
			Console.WriteLine ("UpdateCutOut");
			Console.WriteLine ("BottomToTopTransform");
			Console.WriteLine (BottomToTopTransform);
			Console.WriteLine ("UIRotationTransform");
			Console.WriteLine (UIRotationTransform);
			var temp = CGAffineTransform.CGRectApplyAffineTransform(RegionOfInterest, BottomToTopTransform);
			Console.WriteLine (temp);
			temp = CGAffineTransform.CGRectApplyAffineTransform (temp, BottomToTopTransform);
			Console.WriteLine (temp);
			Console.WriteLine (temp);
			var cutout = Preview.VideoPreviewLayer.MapToLayerCoordinates (temp);
			Console.WriteLine (cutout);
			var path = UIBezierPath.FromRect (CutoutView.Frame);
			Console.WriteLine (path);
			path.AppendPath (UIBezierPath.FromRect (cutout));
			MaskLayer.Path = path.CGPath;
			var numFrame = cutout;
			numFrame.Y += numFrame.Height;
			//NumberLabelView.Frame = numFrame;
			NumberView.Frame = numFrame;
			//Console.WriteLine (MaskLayer);
			//Console.WriteLine (NumberView);
		}

		public void SetupOrientationAndTransform ()
		{
			Console.WriteLine ("SetupOrientationAndTransform");
			var roi = RegionOfInterest;
			ROIToGlobalTransform = CGAffineTransform.MakeTranslation (roi.X, roi.Y);
			ROIToGlobalTransform.Scale (roi.Width, roi.Height);
			Console.WriteLine ("ROIToGlobalTransform");
			Console.WriteLine (ROIToGlobalTransform);
			switch (CurrentOrientation) {
			case UIDeviceOrientation.LandscapeLeft:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Up;
				UIRotationTransform = CGAffineTransform.MakeIdentity ();
				break;
			case UIDeviceOrientation.LandscapeRight:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Down;
				UIRotationTransform = CGAffineTransform.MakeTranslation (1, 1);
				UIRotationTransform.Rotate ((nfloat)Math.PI);
				break;
			case UIDeviceOrientation.PortraitUpsideDown:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Left;
				UIRotationTransform = CGAffineTransform.MakeTranslation (1, 0);
				UIRotationTransform.Rotate ((nfloat)Math.PI / 2);
				break;
			default:
				TextOrientation = ImageIO.CGImagePropertyOrientation.Right;
				UIRotationTransform = CGAffineTransform.MakeTranslation (0, 1);
				UIRotationTransform.Rotate (- (nfloat)Math.PI / 2);
				break;
			}
			//VisionToAVFTransform = { ROIToGlobalTransform , BottomToTopTransform , UIRotationTransform};
			//VisionToAVFTransform = //concats of ROIToGlobalTransform +BottomToTopTransform + UIRotationTransform;
		}

		public void SetupCamera ()
		{
			Console.WriteLine ("SetupCamera");
			//var captureDevice = AVCaptureDevice.GetDefaultDevice (AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, AVCaptureDevicePosition.Back); //null check
			var captureDevice = AVCaptureDevice.GetDefaultDevice (AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, AVCaptureDevicePosition.Back); // guard
			CaptureDevice = captureDevice;
			if (captureDevice.SupportsAVCaptureSessionPreset(AVCaptureSession.Preset3840x2160)) {
				captureSession.SessionPreset = AVCaptureSession.Preset3840x2160;
				BufferAspectRatio = 3840.0 / 2160.0;
			} else {
				captureSession.SessionPreset = AVCaptureSession.Preset1920x1080;
				BufferAspectRatio = 1920.0 / 1080.0;
			}

			var deviceInput = new AVCaptureDeviceInput (captureDevice, out NSError err);  //check error
			if (captureSession.CanAddInput (deviceInput))
				captureSession.AddInput (deviceInput);

			VideoDataOutput.AlwaysDiscardsLateVideoFrames = true;
			VideoDataOutput.SetSampleBufferDelegateQueue (this, VideoDataOutputQueue);
			//VideoDataOutput.WeakVideoSettings = new NSDictionary<NSString, NSString> ();
			//VideoDataOutput.WeakVideoSettings.TryAdd<NSString, NSString> (CVPixelBuffer.PixelFormatTypeKey, new NSString ());
			//{
			//	CVPixelBuffer.PixelFormatTypeKey: new NSString()
			//};
			//VideoDataOutput.WeakVideoSettings = new NSDictionary<> // what the hell is happening?? CVPixelBuffer | PixelFormatTypeKey 
			//VideoDataOutput.WeakVideoSettings = new NSDictionary<NSString, NSString/OSType> () {
			//};

			if (captureSession.CanAddOutput(VideoDataOutput)) {
				captureSession.AddOutput (VideoDataOutput);
				VideoDataOutput.ConnectionFromMediaType (AVMediaType.Video).PreferredVideoStabilizationMode = AVCaptureVideoStabilizationMode.Off;
			} else {
				Console.WriteLine ("Could not add VDO output");
			}

			captureDevice.LockForConfiguration (out NSError lockConf); //check error // try catch
			captureDevice.VideoZoomFactor = 2;
			captureDevice.AutoFocusRangeRestriction = AVCaptureAutoFocusRangeRestriction.Near;
			captureDevice.UnlockForConfiguration ();

			captureSession.StartRunning ();
		}

		public void ShowString (string str)
		{
			Console.WriteLine ("ShowString");
			DispatchQueue.MainQueue.DispatchSync (() => {
				captureSession.StopRunning ();
			});
			DispatchQueue.MainQueue.DispatchSync (() => {
				//NumberLabelView.Text = str;
				//NumberLabelView.Hidden = false;
				NumberView.Text = str;
				NumberView.Hidden = false;
			});
		}

		private void HandleTap ()
		{
			Console.WriteLine ("HandleTap");
			DispatchQueue.MainQueue.DispatchAsync (() => { //make asynch
				//NumberLabelView.Hidden = true;
				NumberView.Hidden = true;
				if (!captureSession.Running) {
					captureSession.StartRunning ();
				}
			});
		}
	}


	// what's the point of this??
	//public static class AVCaptureVideoOrientationExtension {
	//	public static AVCaptureVideoOrientation Init(this AVCaptureVideoOrientation aVCapture, UIDeviceOrientation orientation)
	//	{
	//		switch (orientation) {
	//		case UIDeviceOrientation.Portrait:
	//			aVCapture = AVCaptureVideoOrientation.Portrait;
	//			break;
	//		case UIDeviceOrientation.PortraitUpsideDown:
	//			aVCapture = AVCaptureVideoOrientation.PortraitUpsideDown;
	//			break;
	//		case UIDeviceOrientation.LandscapeLeft:
	//			aVCapture = AVCaptureVideoOrientation.LandscapeRight;
	//			break;
	//		case UIDeviceOrientation.LandscapeRight:
	//			aVCapture = AVCaptureVideoOrientation.LandscapeLeft;
	//			break;
	//		}
	//		return aVCapture;
	//	}
	//}

}