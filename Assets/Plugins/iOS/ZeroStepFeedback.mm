#import <UIKit/UIKit.h>

// strength 0~1: 약한 값은 Light, 중간은 Medium, 강한 값은 Heavy 스타일로 나눠 이동 틱·거부·실패·클리어가 구분되게 한다.
extern "C" void ZeroStepFeedbackImpact(float strength)
{
    if (@available(iOS 13.0, *))
    {
        float clamped = MAX(0.0f, MIN(1.0f, strength));
        dispatch_async(dispatch_get_main_queue(), ^{
            static UIImpactFeedbackGenerator *lightFeedback;
            static UIImpactFeedbackGenerator *mediumFeedback;
            static UIImpactFeedbackGenerator *heavyFeedback;
            if (lightFeedback == nil)
            {
                lightFeedback = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
                mediumFeedback = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
                heavyFeedback = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
            }

            UIImpactFeedbackGenerator *feedback = clamped < 0.34f ? lightFeedback : (clamped < 0.72f ? mediumFeedback : heavyFeedback);
            float intensity = MAX(0.35f, clamped);
            [feedback impactOccurredWithIntensity:intensity];
            [feedback prepare];
        });
    }
}

// 손쉬운 사용 > 동작 줄이기가 켜져 있으면 true. 효과 강도 기본값을 Reduced로 두는 데 쓴다.
extern "C" bool ZeroStepIsReduceMotionEnabled()
{
    return UIAccessibilityIsReduceMotionEnabled();
}
