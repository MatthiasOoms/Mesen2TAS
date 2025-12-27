#include "Common.h"
#include "Core/Shared/Emulator.h"
#include "Core/Shared/Video/VideoRenderer.h"
#include "Core/Shared/Audio/SoundMixer.h"
#include "Core/Shared/Movies/MovieManager.h"
#include "Core/Shared/RewindManager.h"
#include <combaseapi.h>

extern unique_ptr<Emulator> _emu;

extern "C"
{
	DllExport void __stdcall AviRecord(char* filename, RecordAviOptions options) { _emu->GetVideoRenderer()->StartRecording(filename, options); }
	DllExport void __stdcall AviStop() { _emu->GetVideoRenderer()->StopRecording(); }
	DllExport bool __stdcall AviIsRecording() { return _emu->GetVideoRenderer()->IsRecording(); }

	DllExport void __stdcall WaveRecord(char* filename) { _emu->GetSoundMixer()->StartRecording(filename); }
	DllExport void __stdcall WaveStop() { _emu->GetSoundMixer()->StopRecording(); }
	DllExport bool __stdcall WaveIsRecording() { return _emu->GetSoundMixer()->IsRecording(); }

	DllExport void __stdcall MoviePlay(char* filename) { _emu->GetMovieManager()->Play(string(filename)); }

	// Get number of rows
	DllExport int __stdcall MovieGetInputRowCount()
	{
		return (int)_emu->GetMovieManager()->GetCurrentMovieInput().size();
	}

	// Get number of columns
	DllExport int __stdcall MovieGetInputColCount()
	{
		auto& input = _emu->GetMovieManager()->GetCurrentMovieInput();
		if(input.empty()) return 0;
		return (int)input[0].size();
	}

	// Get a single cell (row, column)
	DllExport const char* __stdcall MovieGetInputCell(int row, int col)
	{
		auto movieManager = _emu->GetMovieManager();
		if(!movieManager->Playing())
		{
			return "";
		}

		auto& input = movieManager->GetCurrentMovieInput();

		if(row < 0 || row >= (int)input.size() || col < 0 || col >= (int)input[row].size()) {
			return "";
		}

		const std::string& value = input[row][col];
		// Allocate memory for C# to free
		char* cstr = (char*)CoTaskMemAlloc(value.size() + 1);
		strcpy_s(cstr, value.size() + 1, value.c_str());
		return cstr;
	}

	DllExport void __stdcall MovieAdvanceFrame() 
	{ 
		_emu->Resume();
		_emu->PauseOnNextFrame();
	}

	DllExport void __stdcall MovieRewindFrame()
	{
		// Go back 2 frames
		auto rewindManager = _emu->GetRewindManager();
		if(rewindManager)
		{
			rewindManager->RewindFrames(2);
		}
	}

	DllExport void __stdcall MoviePause() { _emu->Pause(); }
	DllExport void __stdcall MoviePauseOnNextFrame() { _emu->PauseOnNextFrame(); }
	DllExport void __stdcall MovieResume() { _emu->Resume(); }

	DllExport void __stdcall MovieStop() { _emu->GetMovieManager()->Stop(); }
	DllExport bool __stdcall MoviePlaying() { return _emu->GetMovieManager()->Playing(); }
	DllExport bool __stdcall MovieRecording() { return _emu->GetMovieManager()->Recording(); }
	DllExport void __stdcall MovieRecord(RecordMovieOptions options) { _emu->GetMovieManager()->Record(options); }
}