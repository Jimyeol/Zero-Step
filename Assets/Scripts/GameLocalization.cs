using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임 다국어 텍스트 제공자.
/// - 시스템 언어 기본값 지원
/// - 선택 언어 저장/복원용 코드 정규화 지원
/// - 키 기반 문자열 + 치환 토큰 지원
/// </summary>
public static class GameLocalization
{
    public const string LanguageAuto = "auto";

    private static readonly string[] SelectionOrder =
    {
        LanguageAuto,
        "ko",
        "en",
        "ja",
        "zh-Hans",
        "zh-Hant",
        "es",
        "fr",
        "de",
        "pt",
        "ru",
        "id",
        "th"
    };

    private static readonly HashSet<string> SupportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ko",
        "en",
        "ja",
        "zh-Hans",
        "zh-Hant",
        "es",
        "fr",
        "de",
        "pt",
        "ru",
        "id",
        "th"
    };

    private static readonly Dictionary<string, string> LanguageDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { LanguageAuto, "System" },
        { "ko", "한국어" },
        { "en", "English" },
        { "ja", "日本語" },
        { "zh-Hans", "简体中文" },
        { "zh-Hant", "繁體中文" },
        { "es", "Español" },
        { "fr", "Français" },
        { "de", "Deutsch" },
        { "pt", "Português" },
        { "ru", "Русский" },
        { "id", "Bahasa Indonesia" },
        { "th", "ไทย" }
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Translations =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "en",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "stage_title", "STAGE" },
                    { "settings_title", "Settings" },
                    { "section_game_settings", "Game Settings" },
                    { "section_help_language", "Help & Language" },
                    { "section_recommend_service", "Recommended & Service" },
                    { "section_data_policy", "Data & Policy" },
                    { "label_sound", "Sound" },
                    { "label_vibration", "Vibration" },
                    { "menu_help", "Help" },
                    { "menu_language", "Language: {language}" },
                    { "language_select_title", "Select Language" },
                    { "button_rate", "Rate Us" },
                    { "button_remove_ads", "Remove Ads" },
                    { "button_email", "Contact Developer" },
                    { "button_reset_data", "Reset Data" },
                    { "button_privacy_policy", "Privacy Policy" },
                    { "button_terms", "Terms of Service" },
                    { "reset_confirm_title", "Warning" },
                    { "reset_confirm_message", "All data will be reset. Continue?" },
                    { "button_cancel", "Cancel" },
                    { "button_reset", "Reset" },
                    { "help_generic_title", "Help" },
                    { "help_generic_description", "Connect tiles and reduce all counts to 0." },
                    { "help_close_button", "OK" },
                    { "tutorial_basic_title", "Basic How-To" },
                    { "tutorial_basic_description", "" },
                    { "tutorial_basic_instructions", "1. Connect to a neighboring tile\n   Drag your finger to move to tiles touching up, down, left, or right.\n2. Reduce the tile you leave\n   Counts decrease by 1 when you leave a tile, not when you enter it.\n3. Make every count 0\n   Clear the stage by reducing every tile on the board to 0." },
                    { "tutorial_short_circuit_title", "One-Way Tile" },
                    { "tutorial_short_circuit_description", "You can only leave in the arrow direction, and you cannot enter from that same side." },
                    { "tutorial_short_circuit_instructions", "1. Exit in the arrow direction\n   From this tile, you can move only where the arrow points.\n2. No entry from the arrow side\n   You cannot enter this tile backward from the side the arrow points to." },
                    { "tutorial_short_circuit_hint_intro", "A one-way tile lets you leave only where the arrow points." },
                    { "tutorial_short_circuit_step_exit", "Step on it, then follow the arrow out." },
                    { "tutorial_short_circuit_step_blocked_entry", "That arrow side is also blocked as an entry side." },
                    { "tutorial_short_circuit_step_follow", "After leaving it, continue from the next tile as usual." },
                    { "tutorial_short_circuit_step_remember", "Remember: exit with the arrow, never enter from the arrow side." },
                    { "tutorial_cross_blast_title", "Cross Blast Tile" },
                    { "tutorial_cross_blast_description", "When you leave this tile, tiles in a cross direction lose 1 count. The tile you move into is not hit." },
                    { "tutorial_cross_blast_instructions", "1. The cross direction decreases too\n   When you leave a cross blast tile, tiles in a cross direction lose 1 count.\n2. The tile you move into is excluded\n   The next tile you just moved into is not affected by the blast." },
                    { "tutorial_cross_blast_step_intro", "Cross Blast affects the four neighboring tiles." },
                    { "tutorial_cross_blast_step_blast", "Step on it and the cross-shaped blast charges." },
                    { "tutorial_cross_blast_step_adjacent", "Nearby tiles lose 1 count." },
                    { "tutorial_cross_blast_step_exclude", "The tile you exit into stays safe from the blast." },
                    { "tutorial_fixed_knot_title", "Order Tile" },
                    { "tutorial_fixed_knot_description", "This tile must be stepped on at the exact shown order. Missing that order causes game over." },
                    { "tutorial_fixed_knot_instructions", "1. Step on it at the right order\n   Step on this tile when the displayed order arrives.\n2. Missing the order fails\n   If you pass the required turn or step in the wrong order, it is game over." },
                    { "tutorial_fixed_knot_step_intro", "The number shows the required step order." },
                    { "tutorial_fixed_knot_step_countdown", "As you move, the required order counts down." },
                    { "tutorial_fixed_knot_step_exact", "Step on it exactly when it reaches 1." },
                    { "tutorial_fixed_knot_step_missed", "If the order passes, the stage fails." },
                    { "tutorial_twin_link_title", "Buddy Tile" },
                    { "tutorial_twin_link_description", "Tiles with the same buddy color are linked. Leaving one reduces its buddy too." },
                    { "tutorial_twin_link_instructions", "1. Same color means linked\n   Tiles glowing with the same color belong to one group.\n2. Reduce one and they reduce together\n   When you leave one buddy tile, tiles in the same color group lose count together." },
                    { "tutorial_twin_link_step_intro", "Same-colored buddy tiles are connected." },
                    { "tutorial_twin_link_step_leave", "Move away from one buddy tile." },
                    { "tutorial_twin_link_step_pair", "The linked buddy loses 1 count together." },
                    { "tutorial_twin_link_step_together", "Clear buddies together so neither runs out too early." },
                    { "tutorial_igniter_title", "Switch-On Tile" },
                    { "tutorial_igniter_description", "Stepping on this tile turns on hidden tiles with the same target group." },
                    { "tutorial_igniter_instructions", "1. Turn on hidden tiles\n   Stepping on a switch-on tile reveals hidden tiles.\n2. Continue through the new path\n   Use the revealed tiles to connect paths that were blocked." },
                    { "tutorial_igniter_step_intro", "Hidden tiles wait in the dark." },
                    { "tutorial_igniter_step_trigger", "Step on the switch-on tile." },
                    { "tutorial_igniter_step_reveal", "Hidden tiles light up and become playable." },
                    { "tutorial_igniter_step_continue", "Continue through the newly opened path." },
                    { "tutorial_blind_curtain_title", "Covered Tile" },
                    { "tutorial_blind_curtain_description", "Its count is hidden as ?, but it still decreases like a normal tile when used." },
                    { "tutorial_blind_curtain_instructions", "1. The number is hidden\n   A covered tile shows ?, but it has a real number inside.\n2. It decreases when you pass through\n   Like a normal tile, its hidden count decreases by 1 when you leave it." },
                    { "tutorial_blind_curtain_step_intro", "The count is hidden behind a question mark." },
                    { "tutorial_blind_curtain_step_unknown", "Step on it even though you cannot see the count." },
                    { "tutorial_blind_curtain_step_normal", "It behaves like a normal tile until it disappears." },
                    { "tutorial_blackout_title", "Blackout Tile" },
                    { "tutorial_blackout_description", "Stepping on this tile turns every tile count into ? so you must remember the board." },
                    { "tutorial_blackout_instructions", "1. Every number gets covered\n   When you step on a blackout tile, all board numbers turn into ?.\n2. Move by memory\n   Remember the tile numbers and positions before they are covered." },
                    { "tutorial_blackout_step_intro", "This dark tile hides its own number." },
                    { "tutorial_blackout_step_trigger", "Step on it to trigger blackout." },
                    { "tutorial_blackout_step_flip", "All tile numbers flip into question marks." },
                    { "tutorial_blackout_step_memory", "Use memory to finish the board." },
                    { "snackbar_short_circuit_only_direction", "You can only move {direction}." },
                    { "snackbar_short_circuit_blocked_entry", "You cannot enter from the {direction} side." },
                    { "snackbar_fixed_knot_only_order", "You can only step on this tile at move {order}." },
                    { "snackbar_fixed_knot_missed", "Order tiles must be stepped on at the exact move." },
                    { "snackbar_gameover_no_legal_move", "Game over: no legal move from here." },
                    { "snackbar_gameover_unreachable_remaining_tiles", "Game over: the remaining tiles are cut off from here." },
                    { "snackbar_gameover_hidden_untriggerable", "Game over: hidden tiles remain, but no switch can reveal them." },
                    { "snackbar_gameover_short_circuit_blocked", "Game over: the one-way tile blocks every legal path." },
                    { "snackbar_gameover_twin_link_unsatisfiable", "Game over: linked tiles can no longer decrease together." },
                    { "snackbar_gameover_invalid_current_start", "Game over: this position can no longer continue." },
                    { "snackbar_hint_no_path", "No safe hint path from here." },
                    { "snackbar_hint_no_solution_restart", "No clearable hint from here. Restart the stage." },
                    { "snackbar_hint_solver_timeout", "Hint search took too long. Play a few more moves, then try again." },
                    { "hint_loading", "Finding a clear hint..." },
                    { "snackbar_idle_hint_bonus_granted", "Free hint granted." },
                    { "snackbar_hint_recharging", "Hint is recharging: {time}" },
                    { "snackbar_skip_recharging", "Skip is recharging: {time}" },
                    { "direction_left", "left" },
                    { "direction_right", "right" },
                    { "direction_up", "up" },
                    { "direction_down", "down" },
                    { "tutorial_hint_connect", "Start from the left tile and connect a path." },
                    { "tutorial_step_start", "Start on the left" },
                    { "tutorial_step_left", "Left tile count -1" },
                    { "tutorial_step_center", "Center tile count -1" },
                    { "tutorial_step_right", "Right tile count -1" },
                    { "tutorial_step_clear", "Remaining count 0: Stage Clear!" },
                    { "heart_rewarded_title", "You're out of hearts" },
                    { "heart_rewarded_message", "Watch an ad to refill 5 hearts and restart this stage." },
                    { "heart_rewarded_hint", "Reward: 5 hearts + instant restart" },
                    { "heart_rewarded_button", "Watch Ad to Refill 5 Hearts" },
                    { "heart_session_title", "Free Heart Refill" },
                    { "heart_session_message", "You've played for {minutes} minutes. Get 5 hearts for free." },
                    { "heart_session_hint", "Reward: 5 hearts + instant restart (no ad)" },
                    { "heart_session_button", "Claim Free Refill" },
                    { "heart_status_session_reward", "Free refill available after {minutes} minutes of play." },
                    { "heart_status_reward_ready", "Refill 5 hearts after watching an ad." },
                    { "heart_status_loading_ad", "Loading ad..." },
                    { "heart_status_editor", "Instant refill in Editor." },
                    { "heart_status_opening_ad", "Opening ad..." },
                    { "heart_status_prepare_retry", "Ad is preparing. Please try again shortly." },
                    { "heart_status_load_failed", "Failed to load ad. Please try again shortly." },
                    { "heart_status_no_reward", "No reward received. Please try again." },
                    { "heart_status_show_failed", "Failed to show ad. Please try again." },
                    { "final_credits_intro_title", "ZERO STEP COMPLETE" },
                    { "final_credits_finale_title", "STAGE 1000 COMPLETE" },
                    { "final_credits_finale_subtitle", "THE FINAL STEP IS LIT" },
                    { "final_credits_complete_title", "One Thousand Lights" },
                    { "final_credits_complete_value", "Thank you for reaching the end." },
                    { "final_credits_first_played", "The First Light" },
                    { "final_credits_stage1000_clear", "The Thousandth Step" },
                    { "final_credits_total_play_time", "Time In The Grid" },
                    { "final_credits_total_play_days", "Days Returned" },
                    { "final_credits_days_to_1000", "Journey Length" },
                    { "final_credits_fastest_stage", "Fastest Solve" },
                    { "final_credits_longest_stage", "Longest Thought" },
                    { "final_credits_first_try", "First-Try Clears" },
                    { "final_credits_no_hint", "No-Hint Clears" },
                    { "final_credits_best_streak", "Best Clear Streak" },
                    { "final_credits_best_no_reset", "Best No-Reset Streak" },
                    { "final_credits_most_retried", "Most Replayed Stage" },
                    { "final_credits_total_restarts", "Restarts Chosen" },
                    { "final_credits_total_gameovers", "Game Overs Survived" },
                    { "final_credits_heart_depleted", "Moments All Hearts Went Dark" },
                    { "final_credits_player_title", "Your Title" },
                    { "final_credits_unknown", "Unknown" },
                    { "final_credits_days_value", "{count} day(s)" },
                    { "final_credits_duration_hours", "{hours}h {minutes}m" },
                    { "final_credits_duration_minutes", "{minutes}m" },
                    { "final_credits_stage_seconds", "Stage {stage} · {time}" },
                    { "final_credits_stage_count", "Stage {stage} · {count} time(s)" },
                    { "final_credits_title_neon_master", "NEON MASTER" },
                    { "final_credits_title_intuition", "Intuition Solver" },
                    { "final_credits_title_sprinter", "Neon Sprinter" },
                    { "final_credits_title_indomitable", "Indomitable Player" },
                    { "final_credits_title_calm_solver", "Calm Solver" },
                    { "final_credits_skip", "SKIP" },
                    { "final_credits_main", "New Start" },
                    { "final_credits_replay", "Replay Credits" },
                    { "final_credits_update_button", "Update Coming" },
                    { "final_credits_update_title", "New Content Is Coming" },
                    { "final_credits_update_message", "Infinite mode, daily challenges, and weekly stages are being prepared for a future update." },
                    { "snackbar_default_new_tile_unlock", "A new tile type unlocks soon! {remainingStages} stage(s) left." },
                    { "banner_loading", "AD LOADING..." },
                    { "banner_loading_test", "TEST AD LOADING..." },
                    { "banner_default", "BANNER" },
                    { "banner_load_failed", "AD LOAD FAILED" },
                    { "language_system", "System" }
                }
            },
            {
                "ko",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "stage_title", "STAGE" },
                    { "settings_title", "설정" },
                    { "section_game_settings", "게임 설정" },
                    { "section_help_language", "도움말 · 언어" },
                    { "section_recommend_service", "추천 · 서비스" },
                    { "section_data_policy", "데이터 · 정책" },
                    { "label_sound", "소리" },
                    { "label_vibration", "진동" },
                    { "menu_help", "도움말" },
                    { "menu_language", "언어 변경: {language}" },
                    { "language_select_title", "언어 선택" },
                    { "button_rate", "평가하기" },
                    { "button_remove_ads", "광고 제거" },
                    { "button_email", "개발자에게 이메일 보내기" },
                    { "button_reset_data", "데이터 초기화" },
                    { "button_privacy_policy", "개인정보처리방침" },
                    { "button_terms", "이용약관" },
                    { "reset_confirm_title", "경고" },
                    { "reset_confirm_message", "데이터들이 다 초기화됩니다. 정말 초기화하시겠습니까?" },
                    { "button_cancel", "취소" },
                    { "button_reset", "초기화" },
                    { "help_generic_title", "도움말" },
                    { "help_generic_description", "타일을 연결해 카운트를 0으로 만드세요." },
                    { "help_close_button", "확인" },
                    { "tutorial_basic_title", "기본 플레이 방법" },
                    { "tutorial_basic_description", "" },
                    { "tutorial_basic_instructions", "1. 옆 타일로 이어가기\n   손가락을 끌어서 상하좌우로 붙어 있는 타일로 이동하세요.\n2. 지나간 타일 숫자 줄이기\n   타일에 들어갈 때가 아니라, 그 타일에서 나갈 때 숫자가 1 줄어듭니다.\n3. 모든 숫자 0 만들기\n   보드에 남은 모든 타일 숫자를 0으로 만들면 스테이지를 클리어합니다." },
                    { "tutorial_short_circuit_title", "한방향 타일" },
                    { "tutorial_short_circuit_description", "화살표 방향으로만 나갈 수 있고, 그 방향 쪽에서는 들어올 수 없습니다." },
                    { "tutorial_short_circuit_instructions", "1. 화살표 방향으로 나가기\n   이 타일에서는 화살표가 가리키는 방향으로만 이동할 수 있습니다.\n2. 반대쪽에서는 못 들어와요\n   화살표가 가리키는 쪽에서는 이 타일로 거꾸로 들어올 수 없습니다." },
                    { "tutorial_short_circuit_hint_intro", "한방향 타일은 화살표가 가리키는 쪽으로만 나갈 수 있습니다." },
                    { "tutorial_short_circuit_step_exit", "타일을 밟은 뒤에는 화살표 방향으로 나가세요." },
                    { "tutorial_short_circuit_step_blocked_entry", "화살표 방향 쪽에서는 이 타일로 들어올 수 없습니다." },
                    { "tutorial_short_circuit_step_follow", "타일을 벗어난 뒤에는 다음 타일에서 경로를 이어갈 수 있습니다." },
                    { "tutorial_short_circuit_step_remember", "기억하세요. 나갈 땐 화살표 쪽, 들어올 땐 그쪽이 막혀 있습니다." },
                    { "tutorial_cross_blast_title", "십자폭발 타일" },
                    { "tutorial_cross_blast_description", "이 타일에서 나가면 십자방향으로 타일 숫자가 1 줄어듭니다. 이동한 다음 타일은 폭발에 맞지 않습니다." },
                    { "tutorial_cross_blast_instructions", "1. 십자방향도 줄어들어요\n   십자폭발 타일에서 다른 타일로 이동하면 십자방향으로 타일 숫자가 1씩 줄어듭니다.\n2. 방금 간 타일은 제외\n   방금 이동한 다음 타일은 폭발 효과를 받지 않습니다." },
                    { "tutorial_cross_blast_step_intro", "십자폭발 타일은 주변 네 방향에 영향을 줍니다." },
                    { "tutorial_cross_blast_step_blast", "타일을 밟고 나가면 십자 폭발이 켜집니다." },
                    { "tutorial_cross_blast_step_adjacent", "주변 타일들의 카운트가 1씩 줄어듭니다." },
                    { "tutorial_cross_blast_step_exclude", "내가 이동한 다음 타일은 폭발에서 제외됩니다." },
                    { "tutorial_fixed_knot_title", "순서 타일" },
                    { "tutorial_fixed_knot_description", "표시된 순서에 꼭 밟아야 하는 타일입니다. 순서를 놓치면 게임 오버됩니다." },
                    { "tutorial_fixed_knot_instructions", "1. 정해진 순서에 밟기\n   타일에 표시된 순서가 되었을 때 이 타일을 밟아야 합니다.\n2. 순서를 놓치면 실패\n   밟아야 할 차례를 지나치거나 순서를 틀리면 게임오버가 됩니다." },
                    { "tutorial_fixed_knot_step_intro", "숫자는 꼭 밟아야 하는 순서를 뜻합니다." },
                    { "tutorial_fixed_knot_step_countdown", "이동할수록 필요한 순서가 가까워집니다." },
                    { "tutorial_fixed_knot_step_exact", "1이 되었을 때 정확히 밟아야 합니다." },
                    { "tutorial_fixed_knot_step_missed", "순서가 지나가면 스테이지가 실패합니다." },
                    { "tutorial_twin_link_title", "짝꿍 타일" },
                    { "tutorial_twin_link_description", "같은 색 짝꿍 타일은 연결되어 있습니다. 하나를 떠나면 짝꿍도 함께 카운트가 줄어듭니다." },
                    { "tutorial_twin_link_instructions", "1. 같은 색은 연결된 타일\n   같은 색으로 빛나는 타일들은 하나의 그룹으로 연결되어 있습니다.\n2. 하나를 줄이면 같이 줄어요\n   짝꿍 타일 하나에서 나가면 같은 색 그룹의 타일 숫자도 함께 줄어듭니다." },
                    { "tutorial_twin_link_step_intro", "같은 색 짝꿍 타일은 서로 연결되어 있습니다." },
                    { "tutorial_twin_link_step_leave", "짝꿍 타일 하나에서 이동해보세요." },
                    { "tutorial_twin_link_step_pair", "연결된 짝꿍도 함께 카운트가 줄어듭니다." },
                    { "tutorial_twin_link_step_together", "짝꿍들이 너무 먼저 사라지지 않게 같이 정리하세요." },
                    { "tutorial_igniter_title", "켜기 타일" },
                    { "tutorial_igniter_description", "이 타일을 밟으면 같은 목표 그룹의 숨은 타일들이 켜져서 밟을 수 있게 됩니다." },
                    { "tutorial_igniter_instructions", "1. 숨은 타일 켜기\n   켜기 타일을 밟으면 감춰져 있던 타일들이 보이게 됩니다.\n2. 새로운 길로 이어가기\n   나타난 타일을 이용해서 막혀 있던 길을 이어가세요." },
                    { "tutorial_igniter_step_intro", "숨은 타일들은 처음엔 보이지 않습니다." },
                    { "tutorial_igniter_step_trigger", "켜기 타일을 밟아보세요." },
                    { "tutorial_igniter_step_reveal", "숨은 타일들이 켜지고 밟을 수 있게 됩니다." },
                    { "tutorial_igniter_step_continue", "새로 열린 길을 이어가세요." },
                    { "tutorial_blind_curtain_title", "가림 타일" },
                    { "tutorial_blind_curtain_description", "카운트가 ?로 가려져 있지만, 일반 타일처럼 밟으면 줄어듭니다." },
                    { "tutorial_blind_curtain_instructions", "1. 숫자가 보이지 않아요\n   가림 타일은 숫자가 ?로 보이지만, 안에는 실제 숫자가 숨겨져 있습니다.\n2. 지나가면 숫자가 줄어요\n   일반 타일처럼 이 타일에서 나가면 숨겨진 숫자가 1 줄어듭니다." },
                    { "tutorial_blind_curtain_step_intro", "카운트가 물음표로 가려져 있습니다." },
                    { "tutorial_blind_curtain_step_unknown", "숫자는 안 보여도 밟아서 줄일 수 있습니다." },
                    { "tutorial_blind_curtain_step_normal", "사라질 때까지 일반 타일처럼 이어가면 됩니다." },
                    { "tutorial_blackout_title", "깜깜 타일" },
                    { "tutorial_blackout_description", "이 타일을 밟으면 모든 타일 숫자가 ?로 바뀝니다. 보드를 기억해야 합니다." },
                    { "tutorial_blackout_instructions", "1. 모든 숫자가 가려져요\n   깜깜 타일을 밟으면 보드 위 숫자가 모두 ?로 바뀝니다.\n2. 기억해서 움직이기\n   숫자가 가려지기 전에 타일의 숫자와 위치를 기억해 두세요." },
                    { "tutorial_blackout_step_intro", "깜깜 타일은 자기 숫자도 숨깁니다." },
                    { "tutorial_blackout_step_trigger", "밟으면 깜깜 효과가 시작됩니다." },
                    { "tutorial_blackout_step_flip", "모든 타일 숫자가 물음표로 바뀝니다." },
                    { "tutorial_blackout_step_memory", "기억한 숫자로 남은 경로를 이어가세요." },
                    { "snackbar_short_circuit_only_direction", "{direction}으로만 이동할 수 있습니다." },
                    { "snackbar_short_circuit_blocked_entry", "{direction} 방향에서는 진입할 수 없습니다." },
                    { "snackbar_fixed_knot_only_order", "{order}번째 순서에만 밟을 수 있습니다." },
                    { "snackbar_fixed_knot_missed", "순서 타일은 정해진 순서에 꼭 밟아야 합니다." },
                    { "snackbar_gameover_no_legal_move", "더 이동할 수 없어 게임오버입니다." },
                    { "snackbar_gameover_unreachable_remaining_tiles", "남은 타일로 다시 이어갈 수 없어 게임오버입니다." },
                    { "snackbar_gameover_hidden_untriggerable", "남은 숨은 타일을 켤 수 없어 게임오버입니다." },
                    { "snackbar_gameover_short_circuit_blocked", "한방향 타일 규칙 때문에 길이 막혔습니다." },
                    { "snackbar_gameover_twin_link_unsatisfiable", "짝꿍 타일을 더 이상 함께 줄일 수 없습니다." },
                    { "snackbar_gameover_invalid_current_start", "현재 위치에서 이어갈 수 없어 게임오버입니다." },
                    { "snackbar_hint_no_path", "지금 위치에서는 보여줄 힌트 경로가 없습니다." },
                    { "snackbar_hint_no_solution_restart", "클리어할 수 있는 힌트가 없습니다. 재시작하세요." },
                    { "snackbar_hint_solver_timeout", "힌트 계산이 오래 걸립니다. 몇 칸 더 진행한 뒤 다시 눌러주세요." },
                    { "hint_loading", "클리어 힌트를 찾는 중..." },
                    { "snackbar_idle_hint_bonus_granted", "무료 힌트가 지급되었습니다." },
                    { "snackbar_hint_recharging", "힌트 충전 중입니다: {time}" },
                    { "snackbar_skip_recharging", "스킵 충전 중입니다: {time}" },
                    { "direction_left", "왼쪽" },
                    { "direction_right", "오른쪽" },
                    { "direction_up", "위쪽" },
                    { "direction_down", "아래쪽" },
                    { "tutorial_hint_connect", "왼쪽 타일에서 시작해 경로를 연결해보세요." },
                    { "tutorial_step_start", "왼쪽에서 시작" },
                    { "tutorial_step_left", "왼쪽 타일 카운트 -1" },
                    { "tutorial_step_center", "중앙 타일 카운트 -1" },
                    { "tutorial_step_right", "오른쪽 타일 카운트 -1" },
                    { "tutorial_step_clear", "남은 카운트 0: 스테이지 클리어!" },
                    { "heart_rewarded_title", "하트가 모두 소진됐어요" },
                    { "heart_rewarded_message", "광고를 시청하면 하트 5개가 즉시 충전되고 현재 스테이지가 다시 시작됩니다." },
                    { "heart_rewarded_hint", "보상: 하트 5개 + 즉시 재시작" },
                    { "heart_rewarded_button", "광고 보고 하트 5개 충전" },
                    { "heart_session_title", "무료 하트 충전 기회" },
                    { "heart_session_message", "{minutes}분 이상 플레이했기 때문에 하트 5개를 무료로 충전해드립니다." },
                    { "heart_session_hint", "보상: 하트 5개 + 즉시 재시작 (광고 없음)" },
                    { "heart_session_button", "무료 충전 확인" },
                    { "heart_status_session_reward", "{minutes}분 플레이 보상으로 무료 충전 가능합니다." },
                    { "heart_status_reward_ready", "광고 시청 후 하트 5개 충전" },
                    { "heart_status_loading_ad", "광고를 불러오는 중입니다..." },
                    { "heart_status_editor", "에디터에서는 즉시 충전됩니다." },
                    { "heart_status_opening_ad", "광고를 여는 중입니다..." },
                    { "heart_status_prepare_retry", "광고를 준비 중입니다. 잠시 후 다시 시도해 주세요." },
                    { "heart_status_load_failed", "광고 준비에 실패했습니다. 잠시 후 다시 시도해 주세요." },
                    { "heart_status_no_reward", "보상을 받지 못했습니다. 다시 시도해 주세요." },
                    { "heart_status_show_failed", "광고 표시 실패. 다시 시도해 주세요." },
                    { "final_credits_intro_title", "ZERO STEP COMPLETE" },
                    { "final_credits_finale_title", "STAGE 1000 COMPLETE" },
                    { "final_credits_finale_subtitle", "마지막 스텝이 켜졌습니다" },
                    { "final_credits_complete_title", "천 개의 불빛" },
                    { "final_credits_complete_value", "마지막 스텝까지 함께해줘서 고마워요." },
                    { "final_credits_first_played", "처음 불이 켜진 날" },
                    { "final_credits_stage1000_clear", "천 번째 스텝을 넘은 날" },
                    { "final_credits_total_play_time", "네온 격자 안에서 보낸 시간" },
                    { "final_credits_total_play_days", "다시 돌아온 날들" },
                    { "final_credits_days_to_1000", "1000까지 걸린 여정" },
                    { "final_credits_fastest_stage", "가장 빠른 해결" },
                    { "final_credits_longest_stage", "가장 오래 머문 생각" },
                    { "final_credits_first_try", "첫 시도 클리어" },
                    { "final_credits_no_hint", "힌트 없이 클리어" },
                    { "final_credits_best_streak", "최고 연속 클리어" },
                    { "final_credits_best_no_reset", "리셋 없이 이어간 최고 기록" },
                    { "final_credits_most_retried", "가장 많이 다시 도전한 스테이지" },
                    { "final_credits_total_restarts", "다시 시작한 횟수" },
                    { "final_credits_total_gameovers", "넘어졌다가 다시 선 횟수" },
                    { "final_credits_heart_depleted", "모든 하트가 꺼진 순간" },
                    { "final_credits_player_title", "당신의 칭호" },
                    { "final_credits_unknown", "기록 없음" },
                    { "final_credits_days_value", "{count}일" },
                    { "final_credits_duration_hours", "{hours}시간 {minutes}분" },
                    { "final_credits_duration_minutes", "{minutes}분" },
                    { "final_credits_stage_seconds", "{stage}스테이지 · {time}" },
                    { "final_credits_stage_count", "{stage}스테이지 · {count}회" },
                    { "final_credits_title_neon_master", "NEON MASTER" },
                    { "final_credits_title_intuition", "직감의 해결사" },
                    { "final_credits_title_sprinter", "네온 스프린터" },
                    { "final_credits_title_indomitable", "불굴의 플레이어" },
                    { "final_credits_title_calm_solver", "침착한 해결사" },
                    { "final_credits_skip", "스킵" },
                    { "final_credits_main", "새로시작" },
                    { "final_credits_replay", "크레딧 다시 보기" },
                    { "final_credits_update_button", "업데이트 예정" },
                    { "final_credits_update_title", "새로운 콘텐츠를 준비 중입니다" },
                    { "final_credits_update_message", "무한 모드, 데일리 챌린지, 위클리 스테이지를 이후 업데이트로 준비하고 있습니다." },
                    { "snackbar_default_new_tile_unlock", "새로운 타입의 타일이 열립니다! {remainingStages}스테이지 남았습니다." },
                    { "banner_loading", "광고 로딩 중..." },
                    { "banner_loading_test", "테스트 광고 로딩 중..." },
                    { "banner_default", "배너" },
                    { "banner_load_failed", "광고 로드 실패" },
                    { "language_system", "시스템" }
                }
            },
            {
                "ja",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "設定" },
                    { "menu_language", "言語: {language}" },
                    { "button_rate", "評価する" },
                    { "button_remove_ads", "広告を削除" },
                    { "button_email", "開発者にメール" },
                    { "button_reset_data", "データ初期化" },
                    { "button_privacy_policy", "プライバシーポリシー" },
                    { "button_terms", "利用規約" },
                    { "button_cancel", "キャンセル" },
                    { "button_reset", "初期化" },
                    { "heart_rewarded_title", "ハートがなくなりました" },
                    { "snackbar_default_new_tile_unlock", "新しいタイルがもうすぐ解放! 残り{remainingStages}ステージ" },
                    { "language_system", "システム" }
                }
            },
            {
                "zh-Hans",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "设置" },
                    { "menu_language", "语言: {language}" },
                    { "button_rate", "去评分" },
                    { "button_remove_ads", "移除广告" },
                    { "button_email", "联系开发者" },
                    { "button_reset_data", "重置数据" },
                    { "button_privacy_policy", "隐私政策" },
                    { "button_terms", "服务条款" },
                    { "button_cancel", "取消" },
                    { "button_reset", "重置" },
                    { "heart_rewarded_title", "体力已耗尽" },
                    { "snackbar_default_new_tile_unlock", "新类型方块即将解锁！还剩{remainingStages}关" },
                    { "language_system", "系统" }
                }
            },
            {
                "zh-Hant",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "設定" },
                    { "menu_language", "語言: {language}" },
                    { "button_rate", "評分" },
                    { "button_remove_ads", "移除廣告" },
                    { "button_email", "聯絡開發者" },
                    { "button_reset_data", "重設資料" },
                    { "button_privacy_policy", "隱私權政策" },
                    { "button_terms", "服務條款" },
                    { "button_cancel", "取消" },
                    { "button_reset", "重設" },
                    { "heart_rewarded_title", "愛心已用完" },
                    { "snackbar_default_new_tile_unlock", "新類型方塊即將解鎖！剩下{remainingStages}關" },
                    { "language_system", "系統" }
                }
            },
            {
                "es",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Configuración" },
                    { "menu_language", "Idioma: {language}" },
                    { "button_rate", "Califícanos" },
                    { "button_remove_ads", "Quitar anuncios" },
                    { "button_email", "Contactar al desarrollador" },
                    { "button_reset_data", "Restablecer datos" },
                    { "button_privacy_policy", "Política de privacidad" },
                    { "button_terms", "Términos de servicio" },
                    { "button_cancel", "Cancelar" },
                    { "button_reset", "Restablecer" },
                    { "heart_rewarded_title", "Sin corazones" },
                    { "snackbar_default_new_tile_unlock", "¡Nuevo tipo de ficha pronto! Quedan {remainingStages} niveles." },
                    { "language_system", "Sistema" }
                }
            },
            {
                "fr",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Paramètres" },
                    { "menu_language", "Langue : {language}" },
                    { "button_rate", "Noter" },
                    { "button_remove_ads", "Supprimer les pubs" },
                    { "button_email", "Contacter le développeur" },
                    { "button_reset_data", "Réinitialiser les données" },
                    { "button_privacy_policy", "Politique de confidentialité" },
                    { "button_terms", "Conditions d'utilisation" },
                    { "button_cancel", "Annuler" },
                    { "button_reset", "Réinitialiser" },
                    { "heart_rewarded_title", "Plus de cœurs" },
                    { "snackbar_default_new_tile_unlock", "Nouveau type de tuile bientôt ! {remainingStages} niveau(x) restant(s)." },
                    { "language_system", "Système" }
                }
            },
            {
                "de",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Einstellungen" },
                    { "menu_language", "Sprache: {language}" },
                    { "button_rate", "Bewerten" },
                    { "button_remove_ads", "Werbung entfernen" },
                    { "button_email", "Entwickler kontaktieren" },
                    { "button_reset_data", "Daten zurücksetzen" },
                    { "button_privacy_policy", "Datenschutz" },
                    { "button_terms", "Nutzungsbedingungen" },
                    { "button_cancel", "Abbrechen" },
                    { "button_reset", "Zurücksetzen" },
                    { "heart_rewarded_title", "Keine Herzen mehr" },
                    { "snackbar_default_new_tile_unlock", "Neuer Kacheltyp bald verfügbar! Noch {remainingStages} Stufe(n)." },
                    { "language_system", "System" }
                }
            },
            {
                "pt",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Configurações" },
                    { "menu_language", "Idioma: {language}" },
                    { "button_rate", "Avaliar" },
                    { "button_remove_ads", "Remover anúncios" },
                    { "button_email", "Contatar desenvolvedor" },
                    { "button_reset_data", "Redefinir dados" },
                    { "button_privacy_policy", "Política de privacidade" },
                    { "button_terms", "Termos de serviço" },
                    { "button_cancel", "Cancelar" },
                    { "button_reset", "Redefinir" },
                    { "heart_rewarded_title", "Sem corações" },
                    { "snackbar_default_new_tile_unlock", "Novo tipo de peça em breve! Faltam {remainingStages} fase(s)." },
                    { "language_system", "Sistema" }
                }
            },
            {
                "ru",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Настройки" },
                    { "menu_language", "Язык: {language}" },
                    { "button_rate", "Оценить" },
                    { "button_remove_ads", "Убрать рекламу" },
                    { "button_email", "Связаться с разработчиком" },
                    { "button_reset_data", "Сбросить данные" },
                    { "button_privacy_policy", "Политика конфиденциальности" },
                    { "button_terms", "Условия использования" },
                    { "button_cancel", "Отмена" },
                    { "button_reset", "Сброс" },
                    { "heart_rewarded_title", "Сердца закончились" },
                    { "snackbar_default_new_tile_unlock", "Скоро откроется новый тип плитки! Осталось {remainingStages} этап(ов)." },
                    { "language_system", "Система" }
                }
            },
            {
                "id",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "Pengaturan" },
                    { "menu_language", "Bahasa: {language}" },
                    { "button_rate", "Beri Nilai" },
                    { "button_remove_ads", "Hapus Iklan" },
                    { "button_email", "Hubungi Developer" },
                    { "button_reset_data", "Reset Data" },
                    { "button_privacy_policy", "Kebijakan Privasi" },
                    { "button_terms", "Syarat Layanan" },
                    { "button_cancel", "Batal" },
                    { "button_reset", "Reset" },
                    { "heart_rewarded_title", "Heart habis" },
                    { "snackbar_default_new_tile_unlock", "Tipe tile baru segera terbuka! Tersisa {remainingStages} stage." },
                    { "language_system", "Sistem" }
                }
            },
            {
                "th",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "settings_title", "ตั้งค่า" },
                    { "menu_language", "ภาษา: {language}" },
                    { "button_rate", "ให้คะแนน" },
                    { "button_remove_ads", "ลบโฆษณา" },
                    { "button_email", "ติดต่อผู้พัฒนา" },
                    { "button_reset_data", "รีเซ็ตข้อมูล" },
                    { "button_privacy_policy", "นโยบายความเป็นส่วนตัว" },
                    { "button_terms", "ข้อกำหนดการใช้งาน" },
                    { "button_cancel", "ยกเลิก" },
                    { "button_reset", "รีเซ็ต" },
                    { "heart_rewarded_title", "หัวใจหมดแล้ว" },
                    { "snackbar_default_new_tile_unlock", "ไทล์ชนิดใหม่กำลังจะปลดล็อก! เหลืออีก {remainingStages} ด่าน" },
                    { "language_system", "ระบบ" }
                }
            }
        };

    public static string[] GetSelectionOrder()
    {
        string[] copy = new string[SelectionOrder.Length];
        Array.Copy(SelectionOrder, copy, SelectionOrder.Length);
        return copy;
    }

    public static string NormalizeSelectionCode(string selectionCode)
    {
        if (string.IsNullOrWhiteSpace(selectionCode))
            return LanguageAuto;

        string trimmed = selectionCode.Trim();
        if (string.Equals(trimmed, LanguageAuto, StringComparison.OrdinalIgnoreCase))
            return LanguageAuto;

        string normalizedLanguage = NormalizeLanguageCode(trimmed);
        return SupportedLanguages.Contains(normalizedLanguage) ? normalizedLanguage : LanguageAuto;
    }

    public static string ResolveActiveLanguageCode(string selectionCode)
    {
        string normalizedSelection = NormalizeSelectionCode(selectionCode);
        if (string.Equals(normalizedSelection, LanguageAuto, StringComparison.OrdinalIgnoreCase))
            return ResolveSystemLanguageCode();

        return NormalizeLanguageCode(normalizedSelection);
    }

    public static string ResolveSystemLanguageCode()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                return "ko";
            case SystemLanguage.Japanese:
                return "ja";
            case SystemLanguage.ChineseSimplified:
                return "zh-Hans";
            case SystemLanguage.ChineseTraditional:
                return "zh-Hant";
            case SystemLanguage.Chinese:
                return "zh-Hans";
            case SystemLanguage.Spanish:
                return "es";
            case SystemLanguage.French:
                return "fr";
            case SystemLanguage.German:
                return "de";
            case SystemLanguage.Portuguese:
                return "pt";
            case SystemLanguage.Russian:
                return "ru";
            case SystemLanguage.Indonesian:
                return "id";
            case SystemLanguage.Thai:
                return "th";
            case SystemLanguage.English:
            default:
                return "en";
        }
    }

    public static string GetNextSelectionCode(string currentSelectionCode)
    {
        string normalized = NormalizeSelectionCode(currentSelectionCode);
        int currentIndex = 0;
        for (int i = 0; i < SelectionOrder.Length; i++)
        {
            if (string.Equals(SelectionOrder[i], normalized, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + 1) % SelectionOrder.Length;
        return SelectionOrder[nextIndex];
    }

    public static string GetLanguageDisplayName(string selectionOrLanguageCode)
    {
        string normalizedSelection = NormalizeSelectionCode(selectionOrLanguageCode);
        if (LanguageDisplayNames.TryGetValue(normalizedSelection, out string name) && !string.IsNullOrEmpty(name))
            return name;

        string normalizedLanguage = NormalizeLanguageCode(selectionOrLanguageCode);
        if (LanguageDisplayNames.TryGetValue(normalizedLanguage, out name) && !string.IsNullOrEmpty(name))
            return name;

        return "English";
    }

    public static string Get(string key, string activeLanguageCode)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        string normalizedLanguage = NormalizeLanguageCode(activeLanguageCode);

        if (TryGetByLanguageAndKey(normalizedLanguage, key, out string text))
            return text;
        if (TryGetByLanguageAndKey("en", key, out text))
            return text;

        return key;
    }

    public static string Get(string key, string activeLanguageCode, params (string key, string value)[] replacements)
    {
        string text = Get(key, activeLanguageCode);
        if (replacements == null || replacements.Length == 0)
            return text;

        for (int i = 0; i < replacements.Length; i++)
        {
            string token = "{" + replacements[i].key + "}";
            string value = replacements[i].value ?? string.Empty;
            text = text.Replace(token, value);
        }

        return text;
    }

    private static bool TryGetByLanguageAndKey(string languageCode, string key, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(languageCode))
            return false;

        if (!Translations.TryGetValue(languageCode, out Dictionary<string, string> table) || table == null)
            return false;

        return table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "en";

        string code = languageCode.Trim();

        if (string.Equals(code, "zh", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-cn", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-sg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-hans", StringComparison.OrdinalIgnoreCase))
            return "zh-Hans";

        if (string.Equals(code, "zh-tw", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-hk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "zh-hant", StringComparison.OrdinalIgnoreCase))
            return "zh-Hant";

        if (string.Equals(code, "pt-br", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "pt-pt", StringComparison.OrdinalIgnoreCase))
            return "pt";

        string lowered = code.ToLowerInvariant();
        if (SupportedLanguages.Contains(lowered))
            return lowered;

        if (SupportedLanguages.Contains(code))
            return code;

        return "en";
    }
}
