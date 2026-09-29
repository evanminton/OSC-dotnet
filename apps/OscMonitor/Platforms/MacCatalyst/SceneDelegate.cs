using Foundation;

namespace OscMonitor;

// macOS 27 terminates UIKit apps that don't adopt the scene lifecycle, so the app declares a scene delegate in Info.plist.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
