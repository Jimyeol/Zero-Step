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
                    { "tutorial_basic_description", "Move Left(1) -> Center(2) -> Right(1) -> Center to reduce counts and clear." },
                    { "tutorial_short_circuit_title", "ShortCircuit Tile" },
                    { "tutorial_short_circuit_description", "This tile only allows movement in its assigned direction. Check the arrow and continue only that way." },
                    { "tutorial_short_circuit_hint_intro", "ShortCircuit tiles only let you leave in the arrow's direction." },
                    { "tutorial_short_circuit_step_exit", "Start on the ShortCircuit tile, then move exactly where the arrow points." },
                    { "tutorial_short_circuit_step_follow", "After leaving it, continue your path from the next tile as usual." },
                    { "tutorial_short_circuit_step_remember", "Remember: you cannot leave a ShortCircuit tile in any other direction." },
                    { "tutorial_hint_connect", "Start from the left tile and connect a path." },
                    { "tutorial_step_start", "Start on the left" },
                    { "tutorial_step_left", "Left tile count -1" },
                    { "tutorial_step_center", "Center tile count -1" },
                    { "tutorial_step_right", "Right tile count -1" },
                    { "tutorial_step_clear", "Remaining count 0: Stage Clear!" },
                    { "heart_rewarded_title", "You're out of hearts" },
                    { "heart_rewarded_message", "Watch an ad to refill 3 hearts and restart this stage." },
                    { "heart_rewarded_hint", "Reward: 3 hearts + instant restart" },
                    { "heart_rewarded_button", "Watch Ad to Refill 3 Hearts" },
                    { "heart_session_title", "Free Heart Refill" },
                    { "heart_session_message", "You've played for {minutes} minutes. Get 3 hearts for free." },
                    { "heart_session_hint", "Reward: 3 hearts + instant restart (no ad)" },
                    { "heart_session_button", "Claim Free Refill" },
                    { "heart_status_session_reward", "Free refill available after {minutes} minutes of play." },
                    { "heart_status_reward_ready", "Refill 3 hearts after watching an ad." },
                    { "heart_status_loading_ad", "Loading ad..." },
                    { "heart_status_editor", "Instant refill in Editor." },
                    { "heart_status_opening_ad", "Opening ad..." },
                    { "heart_status_prepare_retry", "Ad is preparing. Please try again shortly." },
                    { "heart_status_load_failed", "Failed to load ad. Please try again shortly." },
                    { "heart_status_no_reward", "No reward received. Please try again." },
                    { "heart_status_show_failed", "Failed to show ad. Please try again." },
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
                    { "tutorial_basic_description", "왼쪽(1) → 중앙(2) → 오른쪽(1) → 중앙으로 이동하면 카운트가 줄어들며 클리어됩니다." },
                    { "tutorial_short_circuit_title", "ShortCircuit 타일" },
                    { "tutorial_short_circuit_description", "이 타일은 정해진 방향으로만 이동할 수 있습니다. 화살표 방향을 확인하고 그 방향으로만 이어서 이동하세요." },
                    { "tutorial_short_circuit_hint_intro", "ShortCircuit 타일에서는 화살표가 가리키는 방향으로만 나갈 수 있습니다." },
                    { "tutorial_short_circuit_step_exit", "ShortCircuit 타일에서 시작한 뒤, 화살표가 가리키는 방향으로만 이동하세요." },
                    { "tutorial_short_circuit_step_follow", "해당 타일을 벗어난 뒤에는 다음 타일에서 일반적으로 경로를 이어갈 수 있습니다." },
                    { "tutorial_short_circuit_step_remember", "핵심은 같습니다. ShortCircuit 타일에서는 다른 방향으로 나갈 수 없습니다." },
                    { "tutorial_hint_connect", "왼쪽 타일에서 시작해 경로를 연결해보세요." },
                    { "tutorial_step_start", "왼쪽에서 시작" },
                    { "tutorial_step_left", "왼쪽 타일 카운트 -1" },
                    { "tutorial_step_center", "중앙 타일 카운트 -1" },
                    { "tutorial_step_right", "오른쪽 타일 카운트 -1" },
                    { "tutorial_step_clear", "남은 카운트 0: 스테이지 클리어!" },
                    { "heart_rewarded_title", "하트가 모두 소진됐어요" },
                    { "heart_rewarded_message", "광고를 시청하면 하트 3개가 즉시 충전되고 현재 스테이지가 다시 시작됩니다." },
                    { "heart_rewarded_hint", "보상: 하트 3개 + 즉시 재시작" },
                    { "heart_rewarded_button", "광고 보고 하트 3개 충전" },
                    { "heart_session_title", "무료 하트 충전 기회" },
                    { "heart_session_message", "{minutes}분 이상 플레이했기 때문에 하트 3개를 무료로 충전해드립니다." },
                    { "heart_session_hint", "보상: 하트 3개 + 즉시 재시작 (광고 없음)" },
                    { "heart_session_button", "무료 충전 확인" },
                    { "heart_status_session_reward", "{minutes}분 플레이 보상으로 무료 충전 가능합니다." },
                    { "heart_status_reward_ready", "광고 시청 후 하트 3개 충전" },
                    { "heart_status_loading_ad", "광고를 불러오는 중입니다..." },
                    { "heart_status_editor", "에디터에서는 즉시 충전됩니다." },
                    { "heart_status_opening_ad", "광고를 여는 중입니다..." },
                    { "heart_status_prepare_retry", "광고를 준비 중입니다. 잠시 후 다시 시도해 주세요." },
                    { "heart_status_load_failed", "광고 준비에 실패했습니다. 잠시 후 다시 시도해 주세요." },
                    { "heart_status_no_reward", "보상을 받지 못했습니다. 다시 시도해 주세요." },
                    { "heart_status_show_failed", "광고 표시 실패. 다시 시도해 주세요." },
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
