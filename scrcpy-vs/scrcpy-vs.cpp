// scrcpy-vs.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//

#include <iostream>
#include "config.h"
#include <Windows.h>
#define SCRCPY_DLL
#include "scrcpy-vs.h"


extern "C" int main1(int argc, char *argv[]);
extern"C" void QuitProcess();
FunVideoImageArrive funcVideoArrive = NULL;

extern "C" int video_arrive(byte* image_buff ,int buff_size) {
  if (funcVideoArrive) {
    funcVideoArrive(image_buff, buff_size);
    return true;
  }
  return false;
}
extern "C" int window_need_show() {
    return funcVideoArrive == NULL;
}

HANDLE run_event= NULL;
extern "C" __declspec(dllexport) int __stdcall RunScrcpy(int argc, char *argv[])
{
  if (run_event) {
    return -1;
  }
  run_event = CreateEvent(NULL, FALSE,FALSE, "scrcpy_run" );
  auto ret = main1(argc,argv);
  SetEvent(run_event);
  return ret;
}

extern "C" __declspec(dllexport) void __stdcall CloseScrcpy(int wait_exit_time)
{
  QuitProcess();
  if (run_event) {
    WaitForSingleObject(run_event, wait_exit_time);
    CloseHandle(run_event);
    run_event = NULL;
  }
  return;
}

extern "C" __declspec(dllexport) int __stdcall RegistVideoCB(FunVideoImageArrive func)
{
  funcVideoArrive = func;
  return 1;
}

