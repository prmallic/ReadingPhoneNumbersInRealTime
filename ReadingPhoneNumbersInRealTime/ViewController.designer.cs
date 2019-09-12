// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace ReadingPhoneNumbersInRealTime {
	[Register ("ViewController")]
	partial class ViewController {
		[Outlet]
		public UIKit.UIView CutoutView { get; private set; }

		[Outlet]
		public UIKit.UILabel NumberView { get; private set; }

		[Outlet]
		public ReadingPhoneNumbersInRealTime.PreviewView Preview { get; private set; }

		void ReleaseDesignerOutlets ()
		{
			if (Preview != null) {
				Preview.Dispose ();
				Preview = null;
			}

			if (CutoutView != null) {
				CutoutView.Dispose ();
				CutoutView = null;
			}

			if (NumberView != null) {
				NumberView.Dispose ();
				NumberView = null;
			}
		}
	}
}
