#pragma once

#ifdef SCRCPY_DLL
#define SCRCPY_API extern "C" _declspec(dllexport)
#else 
#define SCRCPY_API extern "C" _declspec(dllimport) 
#endif  
typedef unsigned char byte;
typedef   int(__stdcall *FunVideoImageArrive)(byte* image_buff, int buff_size);
typedef   int(__stdcall *FunScrcpyLog)(int category, int priority, const char *message);

SCRCPY_API int __stdcall RegistVideoCB(FunVideoImageArrive func);
SCRCPY_API int __stdcall RegistScrcpyLogCB(FunScrcpyLog func);
SCRCPY_API int __stdcall RunScrcpy(int argc, char *argv[]);
SCRCPY_API void __stdcall CloseScrcpy(int wait_exit_time);
SCRCPY_API int __stdcall SetADBFolderPath(char *adb_path, char *srv_path);
