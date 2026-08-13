#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Чинит T-позу у персонажей-учителей.
///
/// ЧТО БЫЛО. Модели учителей приходят из Blender со скелетом Quaternius:
/// кости называются Hips, Torso, Shoulder.L, UpperArm.L и так далее.
/// Анимации — из Mixamo, там кости зовутся mixamorig:Hips и так далее.
/// Разные имена — это нормально: режим Humanoid для того и существует,
/// чтобы переносить движение между разными скелетами.
///
/// Но чтобы Humanoid заработал, Unity должна знать, какая кость модели
/// какой части тела соответствует. Обычно она угадывает это сама по именам.
/// На этом скелете угадать не вышло — мешают лишние кости Root, Body,
/// Abdomen выше таза и блендеровские суффиксы .L/.R.
///
/// Результат: у Animator есть контроллер и клипы, но нет рабочего аватара.
/// Переносить движение не на что, персонаж стоит в T-позе, и Unity при этом
/// молчит — ошибки в консоли не будет. Поэтому это так трудно найти.
///
/// ЧТО ДЕЛАЕТ ЭТОТ ФАЙЛ. Проставляет соответствие костей вручную и
/// переимпортирует модели. После этого аватар собирается, и анимации Mixamo
/// начинают переноситься на учителей.
///
/// Меню: Tools → Runner → Персонажи — починить скелет (T-поза)
/// Запускать один раз. Повторный запуск безвреден.
/// </summary>
public static class RunnerAvatarFixer
{
    private const string CharactersFolder = "Assets/Resources/CampusRush/Characters";

    /// <summary>
    /// Кость Unity → кость в модели. Слева — имена, которых ждёт Unity,
    /// справа — как они называются в скелете Quaternius.
    /// </summary>
    private static readonly Dictionary<string, string> BoneMap = new Dictionary<string, string>
    {
        // Позвоночник. Внимание: в этом скелете кость «Hips» — НЕ таз.
        // Настоящий таз называется Body: именно от него отходят и ноги,
        // и позвоночник. «Hips» сидит уже выше и является поясницей.
        // Кость Abdomen намеренно пропущена: Unity держит только три
        // ступени позвоночника, а пропускать промежуточные кости можно.
        { "Hips",       "Body"     },
        { "Spine",      "Hips"     },
        { "Chest",      "Torso"    },
        { "UpperChest", "Chest"    },
        { "Neck",       "Neck"     },
        { "Head",       "Head"     },

        // Ноги
        { "LeftUpperLeg",  "UpperLeg.L" },
        { "LeftLowerLeg",  "LowerLeg.L" },
        { "LeftFoot",      "Foot.L"     },
        { "RightUpperLeg", "UpperLeg.R" },
        { "RightLowerLeg", "LowerLeg.R" },
        { "RightFoot",     "Foot.R"     },
        // Пальцев ног в этом скелете нет. Кости PT.L и PT.R — не носки,
        // а «полюсные цели» (pole target): служебные кости IK-скелета,
        // которые задают, куда смотрит колено. В Humanoid их брать нельзя.

        // Руки
        { "LeftShoulder",  "Shoulder.L" },
        { "LeftUpperArm",  "UpperArm.L" },
        { "LeftLowerArm",  "LowerArm.L" },
        { "LeftHand",      "Wrist.L"    },
        { "RightShoulder", "Shoulder.R" },
        { "RightUpperArm", "UpperArm.R" },
        { "RightLowerArm", "LowerArm.R" },
        { "RightHand",     "Wrist.R"    },

        // Пальцы. Не обязательны для бега, но с ними ладонь не выглядит
        // деревянной в крупном плане после смерти.
        { "LeftThumbProximal",       "Thumb1.L"  },
        { "LeftThumbIntermediate",   "Thumb2.L"  },
        { "LeftThumbDistal",         "Thumb3.L"  },
        { "LeftIndexProximal",       "Index1.L"  },
        { "LeftIndexIntermediate",   "Index2.L"  },
        { "LeftIndexDistal",         "Index3.L"  },
        { "LeftMiddleProximal",      "Middle1.L" },
        { "LeftMiddleIntermediate",  "Middle2.L" },
        { "LeftMiddleDistal",        "Middle3.L" },
        { "LeftRingProximal",        "Ring1.L"   },
        { "LeftRingIntermediate",    "Ring2.L"   },
        { "LeftRingDistal",          "Ring3.L"   },
        { "LeftLittleProximal",      "Pinky1.L"  },
        { "LeftLittleIntermediate",  "Pinky2.L"  },
        { "LeftLittleDistal",        "Pinky3.L"  },
        { "RightThumbProximal",      "Thumb1.R"  },
        { "RightThumbIntermediate",  "Thumb2.R"  },
        { "RightThumbDistal",        "Thumb3.R"  },
        { "RightIndexProximal",      "Index1.R"  },
        { "RightIndexIntermediate",  "Index2.R"  },
        { "RightIndexDistal",        "Index3.R"  },
        { "RightMiddleProximal",     "Middle1.R" },
        { "RightMiddleIntermediate", "Middle2.R" },
        { "RightMiddleDistal",       "Middle3.R" },
        { "RightRingProximal",       "Ring1.R"   },
        { "RightRingIntermediate",   "Ring2.R"   },
        { "RightRingDistal",         "Ring3.R"   },
        { "RightLittleProximal",     "Pinky1.R"  },
        { "RightLittleIntermediate", "Pinky2.R"  },
        { "RightLittleDistal",       "Pinky3.R"  },
    };

    [MenuItem("Tools/Runner/Персонажи — починить скелет (T-поза)")]
    public static void FixAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[RunnerAvatarFixer] В {CharactersFolder} моделей не найдено.");
            return;
        }

        int ok = 0, failed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Fix(path)) ok++; else failed++;
        }

        AssetDatabase.SaveAssets();

        string tail = failed == 0
            ? "Запусти игру — учителя должны побежать."
            : "Часть моделей не собралась. Смотри жёлтые строки выше: там написано, " +
              "какой кости не хватило.";

        Debug.Log($"[RunnerAvatarFixer] Готово. Собрано аватаров: {ok}, не вышло: {failed}. {tail}");
    }

    private static bool Fix(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return false;

        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        // ШАГ 1. Сброс.
        //
        // Unity запоминает раскладку костей модели и при следующем импорте
        // сверяет файл с запомненным. Если скелет в файле поменяли (а мы его
        // как раз починили в Blender), она ругается, что родитель кости
        // «не тот, что был раньше», и отказывается собирать аватар.
        //
        // Лечится тем, что модель сначала импортируется как обычный скелет
        // с пустым описанием: старая раскладка забывается, новая читается
        // из файла заново.
        importer.animationType = ModelImporterAnimationType.Generic;

        HumanDescription clean = importer.humanDescription;
        clean.human = new HumanBone[0];
        clean.skeleton = new SkeletonBone[0];
        importer.humanDescription = clean;
        importer.SaveAndReimport();

        // ШАГ 2. Кости читаем уже после сброса — теперь это то, что в файле.
        var present = new HashSet<string>(
            AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Transform>()
                .Select(t => t.name));

        var human = new List<HumanBone>();
        var missing = new List<string>();

        foreach (KeyValuePair<string, string> pair in BoneMap)
        {
            if (!present.Contains(pair.Value))
            {
                if (IsRequired(pair.Key)) missing.Add($"{pair.Key} (ждали кость «{pair.Value}»)");
                continue;
            }

            var bone = new HumanBone
            {
                humanName = pair.Key,
                boneName = pair.Value,
                limit = { useDefaultValues = true }
            };
            human.Add(bone);
        }

        // Проверка родства. Unity требует, чтобы стопа была потомком голени,
        // голень — потомком бедра, и так далее. Если это не так, аватар
        // не соберётся, а сообщение об ошибке будет невнятным.
        var badParent = new List<string>();
        CheckChain(path, "LowerLeg.L", "Foot.L", badParent);
        CheckChain(path, "LowerLeg.R", "Foot.R", badParent);
        CheckChain(path, "Body", "UpperLeg.L", badParent);
        CheckChain(path, "Body", "UpperLeg.R", badParent);

        if (badParent.Count > 0)
        {
            Debug.LogWarning(
                $"[RunnerAvatarFixer] {name}: скелет собран неправильно — " +
                string.Join("; ", badParent) + ".\n" +
                "Это чинится не здесь, а в Blender. Запусти скрипт " +
                "ArtSource/BlenderScripts/fix_character_rig.py, он привяжет " +
                "стопы к голеням и переэкспортирует модели. Подробности — " +
                "в docs/СКЕЛЕТ_ПЕРСОНАЖЕЙ.md.");
            return false;
        }

        if (missing.Count > 0)
        {
            Debug.LogWarning($"[RunnerAvatarFixer] {name}: не хватает обязательных костей — " +
                             string.Join(", ", missing) + ". Модель пропущена.");
            return false;
        }

        // ШАГ 3. Теперь собираем человека.
        //
        // skeleton оставляем пустым намеренно: это список всех костей со
        // всеми их позициями. Если подсунуть сюда старый список, Unity опять
        // начнёт сверять файл с ним. Пустой — значит «прочитай из файла».
        importer.animationType = ModelImporterAnimationType.Human;

        HumanDescription description = importer.humanDescription;
        description.human = human.ToArray();
        description.skeleton = new SkeletonBone[0];
        importer.humanDescription = description;

        importer.SaveAndReimport();

        // Проверка фактом, а не надеждой: аватар либо собрался, либо нет.
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        bool valid = avatar != null && avatar.isValid && avatar.isHuman;

        if (valid) Debug.Log($"[RunnerAvatarFixer] {name}: аватар собран, костей — {human.Count}.");
        else Debug.LogWarning($"[RunnerAvatarFixer] {name}: аватар не собрался. " +
                              "Открой модель в Инспекторе → Rig → Configure и посмотри, " +
                              "какие части тела подсвечены красным.");

        return valid;
    }

    /// <summary>
    /// Проверяет, что кость child действительно является потомком ancestor.
    /// Именно на этом спотыкался импорт: стопы были привязаны не к голеням,
    /// а к корню скелета, рядом с тазом.
    /// </summary>
    private static void CheckChain(string path, string ancestor, string child, List<string> problems)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Transform>().ToList();

        Transform childBone = all.FirstOrDefault(t => t.name == child);
        if (childBone == null) return;

        for (Transform t = childBone.parent; t != null; t = t.parent)
            if (t.name == ancestor) return;

        string actual = childBone.parent != null ? childBone.parent.name : "ничего";
        problems.Add($"«{child}» должна расти из «{ancestor}», а растёт из «{actual}»");
    }

    /// <summary>Обязательные для Humanoid кости. Без них аватар не соберётся.</summary>
    private static bool IsRequired(string humanName)
    {
        switch (humanName)
        {
            case "Hips":
            case "Spine":
            case "Head":
            case "LeftUpperLeg":
            case "LeftLowerLeg":
            case "LeftFoot":
            case "RightUpperLeg":
            case "RightLowerLeg":
            case "RightFoot":
            case "LeftUpperArm":
            case "LeftLowerArm":
            case "LeftHand":
            case "RightUpperArm":
            case "RightLowerArm":
            case "RightHand":
                return true;
            default:
                return false;
        }
    }
}
#endif
