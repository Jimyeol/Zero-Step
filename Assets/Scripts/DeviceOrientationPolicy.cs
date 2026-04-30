using UnityEngine;

public static class DeviceOrientationPolicy
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void ApplyBeforeSplashScreen()
    {
        ApplyPortrait();
    }

    public static void ApplyPortrait()
    {
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.orientation = ScreenOrientation.Portrait;
    }
}
