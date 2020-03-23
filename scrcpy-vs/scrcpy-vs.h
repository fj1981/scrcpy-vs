#pragma once

#ifdef SCRCPY_DLL
#define SCRCPY_API extern "C" _declspec(dllexport)
#else 
#define SCRCPY_API extern "C" _declspec(dllimport) 
#endif  
typedef unsigned char byte;
typedef   int(__stdcall *FunVideoImageArrive)(byte* image_buff, int buff_size);

SCRCPY_API int __stdcall RegistVideoCB(FunVideoImageArrive func);
SCRCPY_API int __stdcall RunScrcpy(int argc, char *argv[]);
SCRCPY_API void __stdcall CloseScrcpy(int wait_exit_time);

