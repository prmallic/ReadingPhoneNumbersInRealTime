// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;
using UIKit;

namespace ReadingPhoneNumbersInRealTime
{
    [Register ("VisionViewController")]
    partial class VisionViewController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIView VisionView { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (VisionView != null) {
                VisionView.Dispose ();
                VisionView = null;
            }
        }
    }
}