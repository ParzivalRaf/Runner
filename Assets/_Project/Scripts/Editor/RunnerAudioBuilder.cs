#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Генерирует звуки игры как обычные .wav файлы в Assets/_Project/Audio.
///
/// Зачем синтезировать, а не скачать: готовые звуки требуют выбора,
/// проверки лицензии и ручного импорта, а до телефона игра пока не дошла.
/// Сгенерированные — это заглушки уровня «слышно, что происходит»:
/// они мгновенно появляются, ничего не стоят и заменяются в одно движение.
///
/// Как заменить на настоящие: положи свой файл в Assets/_Project/Audio
/// и перетащи его в нужное поле AudioManager на объекте GameManager.
/// Код при этом не меняется.
///
/// Меню: Tools → Runner → Звуки → Сгенерировать заново.
/// Существующие файлы перезаписываются, так что свои клипы храни
/// под другими именами.
/// </summary>
public static class RunnerAudioBuilder
{
    private const string AudioFolder = "Assets/_Project/Audio";
    private const int SampleRate = 44100;

    /// <summary>
    /// Сгенерировать звуки, только если их ещё нет. Вызывает сборщик сцены,
    /// чтобы пересборка не затирала подставленные вручную файлы.
    /// </summary>
    public static void EnsureGenerated()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Music_Loop.wav") != null)
            return;

        Generate();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Runner/Звуки — сгенерировать заново")]
    public static void GenerateAll()
    {
        Generate();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Runner",
            "Звуки сгенерированы в Assets/_Project/Audio.\n\n" +
            "Это заглушки. Свои файлы кладутся туда же и перетаскиваются " +
            "в поля AudioManager на объекте GameManager.",
            "Ок");
    }

    private static void Generate()
    {
        EnsureFolder(AudioFolder);

        WriteWav("SFX_Coin", BuildCoin());
        WriteWav("SFX_PowerUp", BuildPowerUp());
        WriteWav("SFX_Jump", BuildJump());
        WriteWav("SFX_Slide", BuildSlide());
        WriteWav("SFX_Crash", BuildCrash());
        WriteWav("SFX_Button", BuildButton());
        WriteWav("Music_Loop", BuildMusicLoop());
    }

    // ============================================================== синтез

    /// <summary>Монета: два коротких блипа подряд, второй выше — «дзынь».</summary>
    private static float[] BuildCoin()
    {
        float[] data = NewBuffer(0.16f);

        AddTone(data, frequency: 988f, start: 0f, duration: 0.05f, amplitude: 0.5f);
        AddTone(data, frequency: 1319f, start: 0.045f, duration: 0.11f, amplitude: 0.5f);

        return Normalize(data);
    }

    /// <summary>Бонус: восходящее арпеджио из трёх нот, звучит как «получил».</summary>
    private static float[] BuildPowerUp()
    {
        float[] data = NewBuffer(0.42f);

        AddTone(data, 523f, 0.00f, 0.12f, 0.42f);
        AddTone(data, 659f, 0.10f, 0.12f, 0.42f);
        AddTone(data, 880f, 0.20f, 0.22f, 0.45f);

        return Normalize(data);
    }

    /// <summary>Прыжок: короткий свип вверх.</summary>
    private static float[] BuildJump()
    {
        float[] data = NewBuffer(0.16f);

        AddSweep(data, fromFrequency: 260f, toFrequency: 700f,
                 start: 0f, duration: 0.15f, amplitude: 0.45f);

        return Normalize(data);
    }

    /// <summary>Подкат: шум с быстрым затуханием — шорох по полу.</summary>
    private static float[] BuildSlide()
    {
        float[] data = NewBuffer(0.28f);

        AddNoise(data, start: 0f, duration: 0.27f, amplitude: 0.30f, lowPass: 0.25f);

        return Normalize(data);
    }

    /// <summary>Столкновение: низкий удар плюс короткий треск.</summary>
    private static float[] BuildCrash()
    {
        float[] data = NewBuffer(0.45f);

        AddSweep(data, 180f, 55f, 0f, 0.35f, 0.60f);
        AddNoise(data, 0f, 0.18f, 0.35f, lowPass: 0.5f);

        return Normalize(data);
    }

    /// <summary>Кнопка: очень короткий сухой щелчок.</summary>
    private static float[] BuildButton()
    {
        float[] data = NewBuffer(0.06f);

        AddTone(data, 1200f, 0f, 0.05f, 0.35f);

        return Normalize(data);
    }

    /// <summary>
    /// Музыка: 16 тактов на 128 ударов в минуту, примерно 30 секунд.
    ///
    /// Структура важнее нот: первые 8 тактов идут без барабанов и без лида,
    /// вторые — в полную. Петля перестаёт ощущаться петлёй, потому что
    /// у неё есть развитие, а не один и тот же такт по кругу.
    ///
    /// Ничего не выходит за границу буфера: иначе на стыке петли был бы щелчок.
    /// </summary>
    private static float[] BuildMusicLoop()
    {
        const float Bpm = 128f;
        const int Bars = 16;

        float beat = 60f / Bpm;
        float bar = beat * 4f;
        float sixteenth = beat * 0.25f;

        float[] data = NewBuffer(bar * Bars);

        // Ля минор → фа → до → соль: самая ходовая последовательность в поп-музыке
        // именно потому, что не приедается.
        float[] bassRoots = { 110.00f, 87.31f, 130.81f, 98.00f };
        float[][] chordNotes =
        {
            new[] { 440.00f, 523.25f, 659.25f, 880.00f },   // Am
            new[] { 349.23f, 440.00f, 523.25f, 698.46f },   // F
            new[] { 523.25f, 659.25f, 783.99f, 1046.50f },  // C
            new[] { 392.00f, 493.88f, 587.33f, 783.99f }    // G
        };

        // Мелодия: индексы нот аккорда. -1 = пауза, они и делают фразу фразой.
        int[] leadPattern = { 0, 2, 1, -1, 2, 3, -1, 1, 0, -1, 2, 1, 3, -1, 2, 0 };

        for (int barIndex = 0; barIndex < Bars; barIndex++)
        {
            float barStart = barIndex * bar;
            int chord = barIndex % 4;
            bool fullSection = barIndex >= 8;   // вторая половина — с барабанами

            float[] notes = chordNotes[chord];
            float root = bassRoots[chord];

            // --- бас: восьмые, каждая четвёртая на октаву выше ---
            for (int step = 0; step < 8; step++)
            {
                float frequency = step % 4 == 3 ? root * 2f : root;
                AddSquare(data, frequency, barStart + step * beat * 0.5f,
                          beat * 0.42f, 0.20f);
            }

            // --- подложка: аккорд длинной мягкой нотой ---
            AddTriangle(data, notes[0] * 0.5f, barStart, bar * 0.92f, 0.09f);
            AddTriangle(data, notes[2] * 0.5f, barStart, bar * 0.92f, 0.07f);

            // --- мелодия: шестнадцатые, только во второй половине ---
            if (fullSection)
            {
                for (int step = 0; step < 16; step++)
                {
                    int note = leadPattern[step];
                    if (note < 0) continue;

                    AddTriangle(data, notes[note], barStart + step * sixteenth,
                                sixteenth * 0.85f, 0.13f);
                }
            }

            // --- барабаны ---
            if (!fullSection && barIndex < 4) continue;   // первые 4 такта совсем пустые

            for (int step = 0; step < 4; step++)
            {
                float beatStart = barStart + step * beat;

                // Бочка на 1 и 3, плюс подхват перед 3 — так грув не стоит на месте.
                if (step == 0 || step == 2) AddKick(data, beatStart, 0.32f);
                if (step == 1) AddKick(data, beatStart + beat * 0.75f, 0.20f);

                // Малый на 2 и 4.
                if (step == 1 || step == 3) AddSnare(data, beatStart, 0.22f);

                if (!fullSection) continue;

                // Хэт на восьмые, слабая доля тише.
                AddHat(data, beatStart, 0.10f);
                AddHat(data, beatStart + beat * 0.5f, 0.06f);
            }
        }

        return Normalize(data, headroom: 0.62f);
    }

    /// <summary>Бочка: быстрый свип вниз. Слышна даже на динамике телефона.</summary>
    private static void AddKick(float[] data, float start, float amplitude)
    {
        AddSweep(data, fromFrequency: 150f, toFrequency: 48f,
                 start: start, duration: 0.13f, amplitude: amplitude);
    }

    /// <summary>Малый барабан: шум плюс короткий тон, чтобы был не просто «пшш».</summary>
    private static void AddSnare(float[] data, float start, float amplitude)
    {
        AddNoise(data, start, 0.13f, amplitude, lowPass: 0.15f);
        AddTone(data, 190f, start, 0.09f, amplitude * 0.5f);
    }

    /// <summary>Хэт: очень короткий яркий шум.</summary>
    private static void AddHat(float[] data, float start, float amplitude)
    {
        AddNoise(data, start, 0.035f, amplitude, lowPass: 0f);
    }

    /// <summary>
    /// Треугольная волна — мягче квадрата и заметнее синуса.
    /// Хорошо звучит как мелодия поверх квадратного баса.
    /// </summary>
    private static void AddTriangle(float[] data, float frequency, float start,
                                    float duration, float amplitude)
    {
        int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, data.Length);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), data.Length - from);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)count;

            // Пила в диапазоне 0..1, сложенная пополам, даёт треугольник -1..1.
            float phase = (t * frequency) % 1f;
            float value = 4f * Mathf.Abs(phase - 0.5f) - 1f;

            data[from + i] += value * amplitude * Envelope(progress);
        }
    }

    // ========================================================= кирпичики синтеза

    private static float[] NewBuffer(float seconds) =>
        new float[Mathf.CeilToInt(seconds * SampleRate)];

    /// <summary>Синус с затуханием. Затухание убирает щелчок в конце ноты.</summary>
    private static void AddTone(float[] data, float frequency, float start,
                                float duration, float amplitude)
    {
        int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, data.Length);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), data.Length - from);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)count;

            data[from + i] += Mathf.Sin(2f * Mathf.PI * frequency * t)
                            * amplitude * Envelope(progress);
        }
    }

    /// <summary>Квадратная волна — заметнее синуса на маленьком динамике телефона.</summary>
    private static void AddSquare(float[] data, float frequency, float start,
                                  float duration, float amplitude)
    {
        int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, data.Length);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), data.Length - from);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float progress = i / (float)count;
            float value = Mathf.Sin(2f * Mathf.PI * frequency * t) >= 0f ? 1f : -1f;

            data[from + i] += value * amplitude * Envelope(progress);
        }
    }

    /// <summary>Скольжение частоты. Фазу копим вручную, иначе будут щелчки.</summary>
    private static void AddSweep(float[] data, float fromFrequency, float toFrequency,
                                 float start, float duration, float amplitude)
    {
        int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, data.Length);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), data.Length - from);

        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float progress = i / (float)count;
            float frequency = Mathf.Lerp(fromFrequency, toFrequency, progress);

            phase += 2f * Mathf.PI * frequency / SampleRate;

            data[from + i] += Mathf.Sin(phase) * amplitude * Envelope(progress);
        }
    }

    /// <summary>
    /// Шум. lowPass — доля «сглаживания»: 0 это резкий белый шум,
    /// ближе к 1 — глухой шорох.
    /// </summary>
    private static void AddNoise(float[] data, float start, float duration,
                                 float amplitude, float lowPass)
    {
        int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, data.Length);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), data.Length - from);

        var random = new System.Random(12345);   // фиксированное зерно: файл воспроизводим
        float previous = 0f;
        float smoothing = Mathf.Clamp01(lowPass);

        for (int i = 0; i < count; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            previous = Mathf.Lerp(white, previous, smoothing);

            float progress = i / (float)count;

            data[from + i] += previous * amplitude * Envelope(progress);
        }
    }

    /// <summary>Быстрая атака, плавный спад. Без неё каждый звук щёлкает.</summary>
    private static float Envelope(float progress)
    {
        const float attack = 0.02f;

        if (progress < attack) return progress / attack;

        float decay = (progress - attack) / (1f - attack);
        return Mathf.Pow(1f - decay, 1.6f);
    }

    /// <summary>Приводим пик к headroom, чтобы сумма голосов не клиппила.</summary>
    private static float[] Normalize(float[] data, float headroom = 0.85f)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));

        if (peak <= 0.0001f) return data;

        float scale = headroom / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= scale;

        return data;
    }

    // ============================================================== запись WAV

    /// <summary>
    /// 16-битный моно PCM — формат, который Unity импортирует без настроек.
    /// </summary>
    private static void WriteWav(string assetName, float[] samples)
    {
        string path = $"{AudioFolder}/{assetName}.wav";
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                        // размер fmt-блока
            writer.Write((short)1);                  // 1 = PCM без сжатия
            writer.Write((short)1);                  // каналов
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);            // байт в секунду
            writer.Write((short)2);                  // байт на кадр
            writer.Write((short)16);                 // бит на отсчёт

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                writer.Write((short)Math.Round(clamped * short.MaxValue));
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string leaf = path.Substring(lastSlash + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
