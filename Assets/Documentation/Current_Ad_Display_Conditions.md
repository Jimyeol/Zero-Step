# Current Ad Display Conditions

Last reviewed: 2026-05-15

This document summarizes the current ad behavior in code, based on `Assets/Scripts/GameMainUIController.cs` and `Assets/Scripts/GameManager.cs`.

## Global Rules

- Real Google Mobile Ads code is compiled only for non-Editor Android/iOS builds:
  - `#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)`
- In Unity Editor or non-mobile platforms:
  - Banner/interstitial/rewarded ads do not actually show.
  - Rewarded flows are treated as successful immediately for testing.
- In Development Build:
  - `Debug.isDebugBuild` is cached as `isDebugBuildCached`.
  - `ShouldDisableAdsForDevelopmentBuild()` returns true.
  - All real ad loading/showing is skipped.
  - Banner area is hidden and ad objects are destroyed.
  - Hint/skip/heart rewarded flows grant the reward without showing an ad.
  - Stage transition interstitial is skipped.
- Release Android/iOS builds:
  - Real ad loading and display can happen.
  - Production AdMob unit IDs are used.
- Test AdMob unit IDs exist in code, but current Development Build logic short-circuits before loading ads, so test IDs are not normally reached while `Debug.isDebugBuild == true`.

## Remove Ads Entitlement

Current entitlement source:

- Key: `RemoveAdsEntitled`
- Loader: `IsRemoveAdsEntitled()`
- Default: `false`

Important current behavior:

- The remove-ads button is still placeholder/debug only.
- The code does not currently set `RemoveAdsEntitled` to true through a purchase flow.
- `RemoveAdsEntitled == true` currently affects hint/skip zero-charge behavior and stage-transition interstitials:
  - Badge shows timer instead of `AD`.
  - Rewarded ad is not shown for hint/skip when charges are 0.
  - Stage-transition interstitial ads are not loaded or shown.
- `RemoveAdsEntitled` does not currently suppress:
  - Banner ads
  - Heart refill rewarded ads

## Banner Ad

Where it starts:

- `InitializeBannerAd()` is called during UI setup.

Shows when all are true:

- Platform is Android or iOS.
- Not Unity Editor.
- Not Development Build.
- A platform banner ad unit ID exists.
- Banner ad loads successfully.

Behavior:

- Uses adaptive anchored banner at bottom.
- `LoadBannerAd()` creates a `BannerView`.
- On load success, `bannerView.Show()` is called.
- Bottom UI reserves banner height while banner is expected/loaded.
- On load failure, reserved banner height is cleared and the placeholder text shows ad load failed.

Does not show when:

- Unity Editor.
- Non-Android/iOS platform.
- Development Build.
- Banner ad unit ID is empty.
- Banner load fails.

Current caveat:

- Remove-ads entitlement does not stop banner loading/showing.

## Hint Rewarded Ad

Triggered by:

- User presses the hint button.

Preconditions before any ad path:

- `GameManager` exists.
- Hint solver is not already running.
- Current state has an available hint path.

Priority order:

1. If idle temporary hint bonus exists:
   - Show hint.
   - Consume temporary bonus.
   - No ad.
2. If normal hint charge exists:
   - Show hint.
   - Consume hint charge.
   - No ad.
3. If Development Build:
   - Log development reward.
   - Show hint.
   - No ad.
4. If remove-ads entitlement is true:
   - Do not show ad.
   - Show recharge snackbar/timer.
5. Otherwise, on Android/iOS release build:
   - Try rewarded ad.
   - On reward earned, show hint.
6. Otherwise, in Editor/non-mobile:
   - Log editor reward.
   - Show hint.
   - No ad.

Current implementation detail:

- Hint rewarded ad uses `TryShowAssistRewardedAd("hint", ...)`.
- That function currently uses the same `stageSkipRewardedAd` instance/ad unit path as stage skip rewarded ads.
- If the assist rewarded ad is not ready:
  - It logs `{assistType}_reward_ad_not_ready`.
  - It loads the stage-skip rewarded ad.
  - It does not grant the hint immediately.

## Stage Skip Rewarded Ad

Triggered by:

- User presses the skip button.

Priority order:

1. If skip charge exists:
   - Immediately load next stage.
   - Consume skip charge.
   - No ad.
2. If Development Build:
   - Log development reward.
   - Immediately load next stage.
   - No ad.
3. If remove-ads entitlement is true:
   - Do not show ad.
   - Show recharge snackbar/timer.
4. Otherwise, on Android/iOS release build:
   - Try rewarded ad.
   - On reward earned and ad closed, load next stage.
5. Otherwise, in Editor/non-mobile:
   - Log editor reward.
   - Immediately load next stage.
   - No ad.

If the rewarded ad is not ready:

- It logs `stage_skip_reward_ad_not_ready`.
- It attempts to load the stage-skip rewarded ad.
- It does not skip immediately.

## Heart Refill Rewarded Ad

Triggered by:

- Heart is consumed on game over or manual retry.
- Current hearts reach 0, or user is already at 0 and needs refill.
- Heart depleted popup opens.

Offer priority:

1. If `GameManager.TryPeekSessionFreeHeartRefill()` has a pending session play reward:
   - Popup uses session reward mode.
   - User can refill hearts without an ad.
2. Otherwise:
   - Popup uses rewarded ad mode.
   - Rewarded ad is loaded if needed.

Shows when all are true:

- User presses the heart refill ad button.
- Platform is Android or iOS.
- Not Unity Editor.
- Not Development Build.
- `rewardedAd != null`.
- `rewardedAd.CanShowAd()` is true.

Reward:

- On reward callback, hearts are refilled to `MaxHearts` which is currently 5.
- The heart depleted popup closes.

Does not show when:

- Session play reward is available and used.
- Unity Editor.
- Non-Android/iOS platform.
- Development Build.
- Rewarded ad is not ready.

If not ready:

- The UI status says the ad is being prepared.
- `LoadRewardedAd()` is called.
- No reward is granted until an ad is shown/rewarded, except in Editor/Development Build test paths.

Current caveat:

- Remove-ads entitlement does not bypass the heart refill rewarded ad path.

## Stage Transition Interstitial Ad

Triggered by:

- Stage clear flow reaches `LoadNextStageAfterDelay()`.
- Before advancing to the next stage, `ShowStageTransitionInterstitialIfNeeded(completedStageIndex, ...)` is called.

Shows only when:

- Completed stage is 12 or higher.
- At least 180 seconds have passed since session start or the last shown stage-transition interstitial.
- At least 5 completed stages have passed since the last shown stage-transition interstitial.
- Remove-ads entitlement is false.
- Not Development Build.
- Platform is Android or iOS.
- Not Unity Editor.
- Interstitial ad is loaded and `CanShowAd()` is true.

Examples:

- Stage 11 clear: not eligible.
- Stage 12+ clear before 180 seconds: not eligible.
- Stage 12+ clear after 180 seconds, with 5+ stages since the previous interstitial, and an already-loaded ad: eligible.
- Stage 15 is no longer special by itself.

If not eligible:

- The completion callback runs immediately.
- Next stage loading continues with no ad.

If eligible but ad is not ready:

- `LoadStageTransitionInterstitialAd()` is called.
- The completion callback runs immediately.
- The player proceeds to the next stage with no ad for that transition.
- Last interstitial time/stage is not updated.

When interstitial opens successfully:

- Last interstitial realtime and completed stage are updated for the current app session.

After interstitial closes or fails:

- The completion callback runs.
- The interstitial is destroyed.
- A new interstitial load is requested unless ads are disabled for Development Build or remove-ads entitlement.

Current caveat:

- Stage-transition interstitial pacing is session-only. App restart resets the in-memory cooldown and stage-gap state.

## Ad Loading Lifecycle

At UI initialization:

- `InitializeBannerAd()` starts ad initialization.
- On real Android/iOS release builds, `MobileAds.Initialize()` queues loading for:
  - Banner
  - Heart rewarded ad
  - Hint/skip rewarded ad
  - Stage transition interstitial

On ad close/fail:

- Heart rewarded ad:
  - Destroy current rewarded ad.
  - Load a new rewarded ad.
- Hint/skip rewarded ad:
  - Destroy current stage-skip rewarded ad.
  - Load a new stage-skip rewarded ad.
- Stage transition interstitial:
  - Destroy current interstitial.
  - Load a new interstitial unless Development Build or remove-ads entitlement disables stage-transition interstitials.

## Current Ad Types Summary

| Ad type | Real display condition | Main reward/effect | Development Build behavior | Editor behavior |
| --- | --- | --- | --- | --- |
| Banner | Android/iOS release, load success | Bottom banner display | Hidden/no load | Placeholder/no real ad |
| Hint rewarded | Hint charge 0, no idle bonus, no remove-ads, hint path exists, ad ready | Show hint | Free hint | Free hint |
| Skip rewarded | Skip charge 0, no remove-ads, ad ready | Skip stage | Free skip | Free skip |
| Heart refill rewarded | Hearts 0, no session free refill, ad ready | Refill to 5 hearts | Free refill | Free refill |
| Stage transition interstitial | Stage 12+, 180s cooldown, 5-stage gap, no remove-ads, ad ready | Show before next stage | Skipped/no load | Skipped |
