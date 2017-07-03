﻿using System;

using Duality.Audio;

namespace Duality.Backend
{
    public interface INativeAudioBuffer : IDisposable
	{
		void LoadData<T>(
			int sampleRate,
			T[] data,
			int dataLength,
			AudioDataLayout dataLayout,
			AudioDataElementType dataElementType) where T : struct;
	}
}