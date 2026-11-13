// Light haptic feedback for Unpaid CEO's interface (called from Haptics.cs on iOS).
#import <UIKit/UIKit.h>

extern "C" void UnpaidHapticsPlay(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind)
        {
            case 0: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; break; }
            case 1: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [g impactOccurred]; break; }
            case 2: { UINotificationFeedbackGenerator *g = [UINotificationFeedbackGenerator new]; [g notificationOccurred:UINotificationFeedbackTypeSuccess]; break; }
            default: { UINotificationFeedbackGenerator *g = [UINotificationFeedbackGenerator new]; [g notificationOccurred:UINotificationFeedbackTypeWarning]; break; }
        }
    });
}
