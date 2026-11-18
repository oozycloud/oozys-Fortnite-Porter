#include <windows.h>
#include <string>
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR args,int){
 wchar_t path[32768];DWORD n=GetModuleFileNameW(nullptr,path,32768);if(!n||n==32768)return 1;
 std::wstring folder(path,n);folder=folder.substr(0,folder.find_last_of(L"\\"));
 std::wstring app=folder+L"\\app\\EmoteStudio.exe",lock=folder+L"\\.emote-studio-install.lock";
 HANDLE h=CreateFileW(lock.c_str(),GENERIC_READ|GENERIC_WRITE,0,nullptr,OPEN_ALWAYS,FILE_ATTRIBUTE_HIDDEN|FILE_FLAG_DELETE_ON_CLOSE,nullptr);
 if(h==INVALID_HANDLE_VALUE){MessageBoxW(nullptr,L"Emote Studio is already running, or its installer is updating this folder.",L"oozy's Emote Studio",MB_OK|MB_ICONINFORMATION);return 2;}
 std::wstring cmd=L"\""+app+L"\" "+args,working=folder+L"\\app";STARTUPINFOW si{};si.cb=sizeof(si);PROCESS_INFORMATION pi{};
 if(!CreateProcessW(app.c_str(),cmd.data(),nullptr,nullptr,FALSE,0,nullptr,working.c_str(),&si,&pi)){CloseHandle(h);MessageBoxW(nullptr,L"The app files could not be opened. Run the Emote Studio installer and choose Repair.",L"oozy's Emote Studio",MB_OK|MB_ICONERROR);return 3;}
 CloseHandle(pi.hThread);WaitForSingleObject(pi.hProcess,INFINITE);DWORD code=1;GetExitCodeProcess(pi.hProcess,&code);CloseHandle(pi.hProcess);CloseHandle(h);return (int)code;
}
