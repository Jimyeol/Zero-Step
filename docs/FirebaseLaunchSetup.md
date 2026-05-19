# Firebase Launch Setup

ZeroStep v1.0.0 uses Firebase for release analytics, crash reporting, BigQuery export, and Remote Config feature/pacing flags.

## Already In Project

- `Assets/google-services.json`
- `Assets/GoogleService-Info.plist`
- Firebase Analytics SDK 13.8.0
- Firebase Crashlytics SDK 13.8.0
- Firebase Remote Config SDK 13.8.0
- `FirebaseBootstrap` runtime initialization and queued event logging

## Firebase Console Checklist

These items cannot be enabled from Unity code. Do them in the Firebase console before production release.

1. Project settings
   - Confirm Android package name matches Unity bundle identifier.
   - Confirm iOS bundle ID matches Unity bundle identifier.
   - Re-download config files if bundle IDs changed.

2. Analytics
   - Enable Google Analytics for the Firebase project.
   - Confirm Android and iOS apps are linked to the same GA4 property.
   - Use event parameters, not per-stage event names.

3. Crashlytics
   - Open Crashlytics once in the console to finish product activation.
   - Build and run one Android/iOS device build.
   - Send a test non-fatal from `FirebaseBootstrap` context menu or a temporary release-candidate test path.
   - Do not ship a forced crash path enabled.

4. BigQuery Export
   - Firebase Console > Project Settings > Integrations > BigQuery.
   - Link the Firebase project to BigQuery.
   - Enable Google Analytics export.
   - Enable Crashlytics export if available for the project.
   - Choose region intentionally because it is hard to change later.

5. Remote Config
   - Firebase Console > Remote Config.
   - Add the parameters listed below.
   - Publish a first template with the same defaults as the app.
   - Keep risky features disabled by default until the next update.

## Remote Config Parameters

| Key | Type | v1.0.0 Default | Purpose |
| --- | --- | ---: | --- |
| `stage_interstitial_first_eligible_stage` | Number | `12` | First stage eligible for stage-transition interstitials. |
| `stage_interstitial_cooldown_seconds` | Number | `180` | Minimum seconds between stage-transition interstitials. |
| `stage_interstitial_min_stage_gap` | Number | `5` | Minimum cleared-stage gap between stage-transition interstitials. |
| `idle_hint_bonus_enabled` | Boolean | `true` | Enables one free idle hint per stage when the state is solvable. |
| `idle_hint_bonus_delay_seconds` | Number | `40` | Idle seconds before checking for the free stage hint. |
| `daily_challenge_enabled` | Boolean | `false` | Future update flag. |
| `weekly_stage_enabled` | Boolean | `false` | Future update flag. |
| `infinite_mode_enabled` | Boolean | `false` | Future update flag. |
| `leaderboard_enabled` | Boolean | `false` | Future update flag. |
| `remote_config_min_fetch_interval_seconds` | Number | `43200` | Production Remote Config fetch cache interval. |

## Analytics Event Shape

Use stable event names with parameters:

- `stage_start`
- `stage_clear`
- `stage_fail`
- `stage_reset`
- `stage_skip`
- `hint_preview_shown`
- `hint_preview_unavailable`
- `hint_charge_use`
- `idle_hint_bonus_granted`
- `idle_hint_bonus_use`
- `heart_consumed`
- `heart_refilled`
- `heart_refill_offer`
- `stage_transition_interstitial_opened`
- `stage_transition_interstitial_complete`
- `remote_config_fetch_complete`
- `remote_config_fetch_failed`

Core parameters:

- `stage_index`
- `reason` or `failure_reason`
- `steps`
- `remaining_count`
- `entry_type`
- `clear_type`
- `moves`
- `cooldown_seconds`
- `stage_gap`
- `app_version`
- `platform`

## Next Update Preparation

Do not enable these in v1.0.0 unless the feature exists:

- Firebase Auth
- App Check
- Firestore
- Cloud Functions
- Play Games Services
- Apple Game Center

For v1.1.0/v1.2.0, add them when daily/weekly/infinite/ranking code is ready.
