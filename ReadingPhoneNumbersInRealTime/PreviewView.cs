using Foundation;
using System;
using UIKit;
using AVFoundation;
using ObjCRuntime;

namespace ReadingPhoneNumbersInRealTime {
	public partial class PreviewView : UIView {
		public AVCaptureVideoPreviewLayer VideoPreviewLayer;

		public PreviewView (IntPtr handle) : base (handle)
		{
			VideoPreviewLayer = (AVCaptureVideoPreviewLayer)Layer;
		}

		public AVCaptureSession Session {
			get => VideoPreviewLayer.Session;
			set => VideoPreviewLayer.Session = value;
		}

		[Export ("layerClass")]
		public static Class GetLayerClass ()
		{
			return new Class (typeof (AVCaptureVideoPreviewLayer));
		}
	}
}