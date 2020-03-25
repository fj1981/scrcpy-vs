// scrcpy-vs.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//

#include <iostream>
#include "config.h"
#include <Windows.h>
#define SCRCPY_DLL
#include "scrcpy-vs.h"


extern "C" int main1(int argc, char *argv[]);
extern"C" void QuitProcess();
FunVideoImageArrive func_video_arrived = NULL;
FunScrcpyLog func_log = NULL;

extern "C" int video_arrive(byte* image_buff ,int buff_size) {
  if (func_video_arrived) {
    func_video_arrived(image_buff, buff_size);
    return true;
  }
  return false;
}

extern "C" int window_need_show() {
    return func_video_arrived == NULL;
}

extern "C" int log_callback(int category, int priority, const char *message) {
  if (func_log) {
    func_log(category,priority,message);
  }
  return 1;
}


extern "C" int has_log_callback() {
  return func_log != NULL;
}


HANDLE run_event= NULL;
SCRCPY_API int __stdcall RunScrcpy(int argc, char *argv[])
{
  if (run_event) {
    return -1;
  }
  run_event = CreateEvent(NULL, FALSE,FALSE, "scrcpy_run" );
  auto ret = main1(argc,argv);
  SetEvent(run_event);
  return ret;
}

SCRCPY_API void __stdcall CloseScrcpy(int wait_exit_time)
{
  QuitProcess();
  if (run_event) {
    WaitForSingleObject(run_event, wait_exit_time);
    CloseHandle(run_event);
    run_event = NULL;
  }
  return;
}

SCRCPY_API int __stdcall RegistVideoCB(FunVideoImageArrive func)
{
  func_video_arrived = func;
  return 1;
}


SCRCPY_API int __stdcall RegistScrcpyLogCB(FunScrcpyLog func)
{
  func_log = func;
  return 1;
}


SCRCPY_API int __stdcall SetADBFolderPath(char *adb_path, char *srv_path)
{
  if(adb_path && adb_path[0]){
     _putenv_s("ADB", adb_path);
  }
  if (srv_path && srv_path[0]) {
    _putenv_s("SCRCPY_SERVER_PATH", srv_path);
  }
  return 1;
}


