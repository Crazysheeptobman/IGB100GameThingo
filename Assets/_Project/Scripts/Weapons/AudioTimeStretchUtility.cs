using UnityEngine;

public static class AudioTimeStretchUtility
{
    private const float MatchToleranceSeconds = 0.025f;

    public static AudioClip CreateStretchedClip(AudioClip source, float targetDuration, string clipName)
    {
        if (source == null || targetDuration <= 0f)
        {
            return source;
        }

        int channels = source.channels;
        int frequency = source.frequency;
        int sourceFrames = source.samples;
        int targetFrames = Mathf.Max(1, Mathf.RoundToInt(targetDuration * frequency));

        if (sourceFrames <= 0 || channels <= 0 || frequency <= 0)
        {
            return source;
        }

        int toleranceFrames = Mathf.RoundToInt(MatchToleranceSeconds * frequency);
        if (Mathf.Abs(targetFrames - sourceFrames) <= toleranceFrames)
        {
            return source;
        }

        float[] sourceData = new float[sourceFrames * channels];
        if (!source.GetData(sourceData, 0))
        {
            return source;
        }

        float[] outputData = StretchGranular(sourceData, sourceFrames, targetFrames, channels, frequency);
        AudioClip stretchedClip = AudioClip.Create(clipName, targetFrames, channels, frequency, false);
        stretchedClip.SetData(outputData, 0);
        return stretchedClip;
    }

    private static float[] StretchGranular(float[] sourceData, int sourceFrames, int targetFrames, int channels, int frequency)
    {
        float[] outputData = new float[targetFrames * channels];
        float[] weights = new float[targetFrames];

        int windowFrames = Mathf.Clamp(Mathf.RoundToInt(frequency * 0.08f), 256, 4096);
        windowFrames = Mathf.Min(windowFrames, sourceFrames, targetFrames);
        windowFrames = Mathf.Max(1, windowFrames);

        int outputHopFrames = Mathf.Max(1, windowFrames / 4);
        float stretchRatio = Mathf.Max(0.0001f, (float)targetFrames / sourceFrames);
        float inputHopFrames = outputHopFrames / stretchRatio;

        int outputStartFrame = 0;
        float inputStartFrame = 0f;

        while (outputStartFrame < targetFrames)
        {
            int sourceStartFrame = Mathf.Clamp(Mathf.RoundToInt(inputStartFrame), 0, Mathf.Max(0, sourceFrames - windowFrames));
            int framesToCopy = Mathf.Min(windowFrames, targetFrames - outputStartFrame);

            for (int frame = 0; frame < framesToCopy; frame++)
            {
                float window = Hann(frame, framesToCopy);
                int sourceIndex = (sourceStartFrame + frame) * channels;
                int outputIndex = (outputStartFrame + frame) * channels;

                for (int channel = 0; channel < channels; channel++)
                {
                    outputData[outputIndex + channel] += sourceData[sourceIndex + channel] * window;
                }

                weights[outputStartFrame + frame] += window;
            }

            outputStartFrame += outputHopFrames;
            inputStartFrame += inputHopFrames;
        }

        for (int frame = 0; frame < targetFrames; frame++)
        {
            float weight = weights[frame];
            if (weight <= 0.00001f)
            {
                continue;
            }

            int outputIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                outputData[outputIndex + channel] /= weight;
            }
        }

        return outputData;
    }

    private static float Hann(int frame, int frameCount)
    {
        if (frameCount <= 1)
        {
            return 1f;
        }

        return 0.5f - 0.5f * Mathf.Cos((2f * Mathf.PI * frame) / (frameCount - 1));
    }
}
