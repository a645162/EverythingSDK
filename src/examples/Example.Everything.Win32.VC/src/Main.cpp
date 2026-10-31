#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <Windows.h>
#include <CommCtrl.h>
#include <shellapi.h>

#include <chrono>
#include <ctime>
#include <string>
#include <vector>

#include "Lib/Everything/Everything.h"

namespace {
const wchar_t WindowClassName[] = L"ExampleEverythingWin32VC.MainWindow";
const int ControlMargin = 8;
const int SearchBarHeight = 28;
const int CheckBoxHeight = 24;
const int StatusBarHeight = 24;
const int LogHeight = 120;
const int TopPanelHeight = 96;

enum ControlId {
  SearchEdit = 1001,
  SearchButton = 1002,
  ResultList = 1003,
  StatusText = 1004,
  VersionText = 1005,
  MatchCaseCheckBox = 1006,
  WholeWordCheckBox = 1007,
  RegexCheckBox = 1008,
  MatchPathCheckBox = 1009,
  SortLabel = 1010,
  SortCombo = 1011,
  MaxLabel = 1012,
  MaxEdit = 1013,
  LogEdit = 1014
};

struct SortItem {
  const wchar_t *Name;
  Lib::Everything::EverythingSort Value;
};

const SortItem SortItems[] = {
    {L"Name Asc", Lib::Everything::EverythingSort::NameAscending},
    {L"Name Desc", Lib::Everything::EverythingSort::NameDescending},
    {L"Path Asc", Lib::Everything::EverythingSort::PathAscending},
    {L"Path Desc", Lib::Everything::EverythingSort::PathDescending},
    {L"Size Asc", Lib::Everything::EverythingSort::SizeAscending},
    {L"Size Desc", Lib::Everything::EverythingSort::SizeDescending},
    {L"Extension Asc", Lib::Everything::EverythingSort::ExtensionAscending},
    {L"Extension Desc", Lib::Everything::EverythingSort::ExtensionDescending},
    {L"Type Asc", Lib::Everything::EverythingSort::TypeNameAscending},
    {L"Type Desc", Lib::Everything::EverythingSort::TypeNameDescending},
    {L"Created Asc", Lib::Everything::EverythingSort::DateCreatedAscending},
    {L"Created Desc", Lib::Everything::EverythingSort::DateCreatedDescending},
    {L"Modified Asc", Lib::Everything::EverythingSort::DateModifiedAscending},
    {L"Modified Desc", Lib::Everything::EverythingSort::DateModifiedDescending},
    {L"Attributes Asc", Lib::Everything::EverythingSort::AttributesAscending},
    {L"Attributes Desc", Lib::Everything::EverythingSort::AttributesDescending},
    {L"FileList Asc",
     Lib::Everything::EverythingSort::FileListFileNameAscending},
    {L"FileList Desc",
     Lib::Everything::EverythingSort::FileListFileNameDescending},
    {L"RunCount Asc", Lib::Everything::EverythingSort::RunCountAscending},
    {L"RunCount Desc", Lib::Everything::EverythingSort::RunCountDescending},
    {L"Recent Asc",
     Lib::Everything::EverythingSort::DateRecentlyChangedAscending},
    {L"Recent Desc",
     Lib::Everything::EverythingSort::DateRecentlyChangedDescending},
    {L"Accessed Asc", Lib::Everything::EverythingSort::DateAccessedAscending},
    {L"Accessed Desc", Lib::Everything::EverythingSort::DateAccessedDescending},
    {L"RunDate Asc", Lib::Everything::EverythingSort::DateRunAscending},
    {L"RunDate Desc", Lib::Everything::EverythingSort::DateRunDescending}};

struct MainWindowState {
  HWND SearchEditHandle;
  HWND SearchButtonHandle;
  HWND ResultListHandle;
  HWND StatusTextHandle;
  HWND VersionTextHandle;
  HWND MatchCaseCheckBoxHandle;
  HWND WholeWordCheckBoxHandle;
  HWND RegexCheckBoxHandle;
  HWND MatchPathCheckBoxHandle;
  HWND SortLabelHandle;
  HWND SortComboHandle;
  HWND MaxLabelHandle;
  HWND MaxEditHandle;
  HWND LogEditHandle;
  std::vector<Lib::Everything::EverythingResult> Results;

  MainWindowState()
      : SearchEditHandle(NULL), SearchButtonHandle(NULL),
        ResultListHandle(NULL), StatusTextHandle(NULL), VersionTextHandle(NULL),
        MatchCaseCheckBoxHandle(NULL), WholeWordCheckBoxHandle(NULL),
        RegexCheckBoxHandle(NULL), MatchPathCheckBoxHandle(NULL),
        SortLabelHandle(NULL), SortComboHandle(NULL), MaxLabelHandle(NULL),
        MaxEditHandle(NULL), LogEditHandle(NULL) {}
};

WNDPROC SearchEditOriginalProc = NULL;

std::wstring GetControlText(HWND handle) {
  const int length = GetWindowTextLengthW(handle);
  std::wstring value(static_cast<std::size_t>(length) + 1, L'\0');
  GetWindowTextW(handle, &value[0], length + 1);
  value.resize(static_cast<std::size_t>(length));
  return value;
}

void AppendLog(MainWindowState &state, const std::wstring &text) {
  SYSTEMTIME localTime = {};
  GetLocalTime(&localTime);

  wchar_t prefix[32] = {};
  wsprintfW(prefix, L"%02u:%02u:%02u  ", localTime.wHour, localTime.wMinute,
            localTime.wSecond);

  const std::wstring line = std::wstring(prefix) + text + L"\r\n";
  const int end = GetWindowTextLengthW(state.LogEditHandle);
  SendMessageW(state.LogEditHandle, EM_SETSEL, end, end);
  SendMessageW(state.LogEditHandle, EM_REPLACESEL, FALSE,
               reinterpret_cast<LPARAM>(line.c_str()));
  SendMessageW(state.LogEditHandle, EM_SCROLLCARET, 0, 0);
}

void SetStatusText(MainWindowState &state, const std::wstring &text) {
  SetWindowTextW(state.StatusTextHandle, text.c_str());
}

std::wstring FormatSize(const Lib::Everything::EverythingResult &result) {
  if (!result.HasSize) {
    return L"-";
  }

  return std::to_wstring(result.Size);
}

std::wstring FormatType(const Lib::Everything::EverythingResult &result) {
  if (result.IsFolder) {
    return L"Folder";
  }

  if (result.IsVolume) {
    return L"Volume";
  }

  if (!result.Extension.empty()) {
    return result.Extension;
  }

  return L"File";
}

std::wstring FormatDateTime(const Lib::Everything::EverythingResult &result) {
  if (!result.HasDateModified) {
    return L"-";
  }

  const std::time_t timeValue =
      std::chrono::system_clock::to_time_t(result.DateModified);
  std::tm localTime = {};
  localtime_s(&localTime, &timeValue);

  wchar_t buffer[32] = {};
  wcsftime(buffer, sizeof(buffer) / sizeof(buffer[0]), L"%Y-%m-%d %H:%M",
           &localTime);
  return std::wstring(buffer);
}

bool IsChecked(HWND handle) {
  return SendMessageW(handle, BM_GETCHECK, 0, 0) == BST_CHECKED;
}

unsigned int ParseMax(HWND handle) {
  const std::wstring text = GetControlText(handle);
  if (text.empty()) {
    return 1000U;
  }

  const unsigned long value = wcstoul(text.c_str(), NULL, 10);
  return value == 0UL ? 1000U : static_cast<unsigned int>(value);
}

Lib::Everything::EverythingSort GetSelectedSort(HWND comboHandle) {
  const LRESULT selected = SendMessageW(comboHandle, CB_GETCURSEL, 0, 0);
  if (selected == CB_ERR) {
    return Lib::Everything::EverythingSort::NameAscending;
  }

  const LRESULT value = SendMessageW(comboHandle, CB_GETITEMDATA,
                                     static_cast<WPARAM>(selected), 0);
  if (value == CB_ERR) {
    return Lib::Everything::EverythingSort::NameAscending;
  }

  return static_cast<Lib::Everything::EverythingSort>(value);
}

void ResizeChildren(HWND windowHandle, MainWindowState &state) {
  RECT clientRect = {};
  GetClientRect(windowHandle, &clientRect);

  const int width = clientRect.right - clientRect.left;
  const int height = clientRect.bottom - clientRect.top;
  const int contentWidth = width - ControlMargin * 2;
  const int searchButtonWidth = 96;
  const int versionWidth = 220;
  const int sortLabelWidth = 36;
  const int sortComboWidth = 160;
  const int maxLabelWidth = 32;
  const int maxEditWidth = 72;
  const int searchEditWidth =
      contentWidth - searchButtonWidth - versionWidth - ControlMargin * 2;

  MoveWindow(state.SearchEditHandle, ControlMargin, ControlMargin,
             searchEditWidth, SearchBarHeight, TRUE);
  MoveWindow(state.SearchButtonHandle,
             ControlMargin + searchEditWidth + ControlMargin, ControlMargin,
             searchButtonWidth, SearchBarHeight, TRUE);
  MoveWindow(state.VersionTextHandle, width - ControlMargin - versionWidth,
             ControlMargin + 4, versionWidth, SearchBarHeight, TRUE);

  MoveWindow(state.MatchCaseCheckBoxHandle, ControlMargin, 44, 120,
             CheckBoxHeight, TRUE);
  MoveWindow(state.WholeWordCheckBoxHandle, 126, 44, 120, CheckBoxHeight, TRUE);
  MoveWindow(state.RegexCheckBoxHandle, 252, 44, 96, CheckBoxHeight, TRUE);
  MoveWindow(state.MatchPathCheckBoxHandle, 354, 44, 110, CheckBoxHeight, TRUE);
  MoveWindow(state.SortLabelHandle, 472, 46, sortLabelWidth, 20, TRUE);
  MoveWindow(state.SortComboHandle, 510, 42, sortComboWidth, 160, TRUE);
  MoveWindow(state.MaxLabelHandle, 682, 46, maxLabelWidth, 20, TRUE);
  MoveWindow(state.MaxEditHandle, 718, 42, maxEditWidth, SearchBarHeight, TRUE);

  MoveWindow(
      state.ResultListHandle, ControlMargin, TopPanelHeight, contentWidth,
      height - TopPanelHeight - LogHeight - StatusBarHeight - ControlMargin * 2,
      TRUE);

  MoveWindow(state.LogEditHandle, ControlMargin,
             height - LogHeight - StatusBarHeight - ControlMargin, contentWidth,
             LogHeight, TRUE);

  MoveWindow(state.StatusTextHandle, ControlMargin, height - StatusBarHeight,
             contentWidth, StatusBarHeight, TRUE);

  if (state.ResultListHandle != NULL) {
    RECT listRect = {};
    GetClientRect(state.ResultListHandle, &listRect);
    const int listWidth = listRect.right - listRect.left;
    ListView_SetColumnWidth(state.ResultListHandle, 0, listWidth - 420);
    ListView_SetColumnWidth(state.ResultListHandle, 1, 120);
    ListView_SetColumnWidth(state.ResultListHandle, 2, 160);
    ListView_SetColumnWidth(state.ResultListHandle, 3, 120);
  }
}

void PopulateResults(MainWindowState &state) {
  ListView_DeleteAllItems(state.ResultListHandle);

  for (int index = 0; index < static_cast<int>(state.Results.size()); ++index) {
    const Lib::Everything::EverythingResult &result =
        state.Results[static_cast<std::size_t>(index)];
    const std::wstring pathText =
        !result.FullPath.empty() ? result.FullPath : result.FileName;
    const std::wstring sizeText = FormatSize(result);
    const std::wstring modifiedText = FormatDateTime(result);
    const std::wstring typeText = FormatType(result);

    LVITEMW item = {};
    item.mask = LVIF_TEXT;
    item.iItem = index;
    item.pszText = const_cast<LPWSTR>(pathText.c_str());
    ListView_InsertItem(state.ResultListHandle, &item);

    ListView_SetItemText(state.ResultListHandle, index, 1,
                         const_cast<LPWSTR>(sizeText.c_str()));
    ListView_SetItemText(state.ResultListHandle, index, 2,
                         const_cast<LPWSTR>(modifiedText.c_str()));
    ListView_SetItemText(state.ResultListHandle, index, 3,
                         const_cast<LPWSTR>(typeText.c_str()));
  }
}

void OpenSelectedResult(MainWindowState &state) {
  const int selectedIndex =
      ListView_GetNextItem(state.ResultListHandle, -1, LVNI_SELECTED);
  if (selectedIndex < 0) {
    return;
  }

  const Lib::Everything::EverythingResult &result =
      state.Results[static_cast<std::size_t>(selectedIndex)];
  if (result.FullPath.empty()) {
    return;
  }

  if (result.IsFolder) {
    ShellExecuteW(NULL, L"open", L"explorer.exe",
                  (L"\"" + result.FullPath + L"\"").c_str(), NULL,
                  SW_SHOWNORMAL);
  } else {
    const std::wstring args = L"/select,\"" + result.FullPath + L"\"";
    ShellExecuteW(NULL, L"open", L"explorer.exe", args.c_str(), NULL,
                  SW_SHOWNORMAL);
  }
}

void PerformSearch(HWND windowHandle, MainWindowState &state) {
  try {
    Lib::Everything::EverythingClient client;
    client.SearchText = GetControlText(state.SearchEditHandle);
    client.MatchCase = IsChecked(state.MatchCaseCheckBoxHandle);
    client.MatchWholeWord = IsChecked(state.WholeWordCheckBoxHandle);
    client.Regex = IsChecked(state.RegexCheckBoxHandle);
    client.MatchPath = IsChecked(state.MatchPathCheckBoxHandle);
    client.Max = ParseMax(state.MaxEditHandle);
    client.Sort = GetSelectedSort(state.SortComboHandle);

    state.Results = client.Query();
    PopulateResults(state);

    const std::wstring message =
        L"Returned " + std::to_wstring(client.NumResults) + L" / " +
        std::to_wstring(client.TotalResults) + L", Error: " +
        Lib::Everything::ToWideString(client.LastError) + L", Max: " +
        std::to_wstring(client.Max);

    SetStatusText(state, message);
    AppendLog(state, L"Query completed: " + message);
  } catch (const std::exception &) {
    SetStatusText(state,
                  L"Query failed. Check Everything.exe and the native DLL.");
    AppendLog(state, L"Query failed.");
    MessageBoxW(windowHandle,
                L"Everything query failed. Make sure Everything.exe is running "
                L"and the native DLL is beside the executable.",
                L"Example.Everything.Win32.VC", MB_OK | MB_ICONERROR);
  }
}

void InitializeSortCombo(HWND comboHandle) {
  for (std::size_t index = 0; index < sizeof(SortItems) / sizeof(SortItems[0]);
       ++index) {
    const LRESULT itemIndex =
        SendMessageW(comboHandle, CB_ADDSTRING, 0,
                     reinterpret_cast<LPARAM>(SortItems[index].Name));
    SendMessageW(comboHandle, CB_SETITEMDATA, static_cast<WPARAM>(itemIndex),
                 static_cast<LPARAM>(SortItems[index].Value));
  }

  SendMessageW(comboHandle, CB_SETCURSEL, 0, 0);
}

void OnLoaded(HWND windowHandle, MainWindowState &state) {
  SetStatusText(state, L"Ready. Make sure Everything.exe is running.");

  try {
    if (!Lib::Everything::EverythingClient::IsEverythingRunning()) {
      MessageBoxW(
          windowHandle,
          L"Everything.exe is not running. Start Everything before searching.",
          L"Everything Not Running", MB_OK | MB_ICONWARNING);
      SetStatusText(state, L"Everything.exe not running.");
    }

    const Lib::Everything::EverythingVersion version =
        Lib::Everything::EverythingClient::GetVersion();
    const std::wstring title =
        L"Example.Everything.Win32.VC - SDK " + version.ToString();
    SetWindowTextW(windowHandle, title.c_str());

    const bool isDbLoaded = Lib::Everything::EverythingClient::IsDBLoaded();
    const std::wstring versionText =
        L"SDK " + version.ToString() +
        (isDbLoaded ? L" | DB Loaded" : L" | DB Not Ready");
    SetWindowTextW(state.VersionTextHandle, versionText.c_str());
    AppendLog(state, L"SDK Version: " + version.ToString());
    AppendLog(state, std::wstring(L"Database Loaded: ") +
                         (isDbLoaded ? L"Yes" : L"No"));
  } catch (const std::exception &) {
    AppendLog(state, L"Initialization check failed.");
    SetWindowTextW(state.VersionTextHandle, L"SDK unavailable");
  }
}

LRESULT CALLBACK SearchEditProc(HWND handle, UINT message, WPARAM wParam,
                                LPARAM lParam) {
  if (message == WM_KEYDOWN && wParam == VK_RETURN) {
    HWND parentHandle = GetParent(handle);
    PostMessageW(
        parentHandle, WM_COMMAND, MAKEWPARAM(SearchButton, BN_CLICKED),
        reinterpret_cast<LPARAM>(GetDlgItem(parentHandle, SearchButton)));
    return 0;
  }

  return CallWindowProcW(SearchEditOriginalProc, handle, message, wParam,
                         lParam);
}

LRESULT CALLBACK WindowProc(HWND windowHandle, UINT message, WPARAM wParam,
                            LPARAM lParam) {
  MainWindowState *state = reinterpret_cast<MainWindowState *>(
      GetWindowLongPtrW(windowHandle, GWLP_USERDATA));

  switch (message) {
  case WM_CREATE: {
    MainWindowState *createdState = new MainWindowState();
    SetWindowLongPtrW(windowHandle, GWLP_USERDATA,
                      reinterpret_cast<LONG_PTR>(createdState));

    createdState->SearchEditHandle = CreateWindowExW(
        WS_EX_CLIENTEDGE, WC_EDITW, L"*.png",
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 0, 0, 0, 0, windowHandle,
        reinterpret_cast<HMENU>(SearchEdit), NULL, NULL);

    createdState->SearchButtonHandle = CreateWindowExW(
        0, WC_BUTTONW, L"Search", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, 0, 0,
        0, 0, windowHandle, reinterpret_cast<HMENU>(SearchButton), NULL, NULL);

    createdState->VersionTextHandle = CreateWindowExW(
        0, WC_STATICW, L"SDK loading", WS_CHILD | WS_VISIBLE | SS_RIGHT, 0, 0,
        0, 0, windowHandle, reinterpret_cast<HMENU>(VersionText), NULL, NULL);

    createdState->MatchCaseCheckBoxHandle = CreateWindowExW(
        0, WC_BUTTONW, L"Match Case", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
        0, 0, 0, 0, windowHandle, reinterpret_cast<HMENU>(MatchCaseCheckBox),
        NULL, NULL);

    createdState->WholeWordCheckBoxHandle = CreateWindowExW(
        0, WC_BUTTONW, L"Whole Word", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
        0, 0, 0, 0, windowHandle, reinterpret_cast<HMENU>(WholeWordCheckBox),
        NULL, NULL);

    createdState->RegexCheckBoxHandle = CreateWindowExW(
        0, WC_BUTTONW, L"Regex", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 0, 0,
        0, 0, windowHandle, reinterpret_cast<HMENU>(RegexCheckBox), NULL, NULL);

    createdState->MatchPathCheckBoxHandle = CreateWindowExW(
        0, WC_BUTTONW, L"Match Path", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
        0, 0, 0, 0, windowHandle, reinterpret_cast<HMENU>(MatchPathCheckBox),
        NULL, NULL);

    createdState->SortLabelHandle = CreateWindowExW(
        0, WC_STATICW, L"Sort", WS_CHILD | WS_VISIBLE, 0, 0, 0, 0, windowHandle,
        reinterpret_cast<HMENU>(SortLabel), NULL, NULL);

    createdState->SortComboHandle = CreateWindowExW(
        0, WC_COMBOBOXW, L"",
        WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL, 0, 0, 0, 0,
        windowHandle, reinterpret_cast<HMENU>(SortCombo), NULL, NULL);

    createdState->MaxLabelHandle = CreateWindowExW(
        0, WC_STATICW, L"Max", WS_CHILD | WS_VISIBLE, 0, 0, 0, 0, windowHandle,
        reinterpret_cast<HMENU>(MaxLabel), NULL, NULL);

    createdState->MaxEditHandle = CreateWindowExW(
        WS_EX_CLIENTEDGE, WC_EDITW, L"1000",
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 0, 0, 0, 0, windowHandle,
        reinterpret_cast<HMENU>(MaxEdit), NULL, NULL);

    createdState->ResultListHandle = CreateWindowExW(
        WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"",
        WS_CHILD | WS_VISIBLE | LVS_REPORT | LVS_SHOWSELALWAYS, 0, 0, 0, 0,
        windowHandle, reinterpret_cast<HMENU>(ResultList), NULL, NULL);

    createdState->LogEditHandle = CreateWindowExW(
        WS_EX_CLIENTEDGE, WC_EDITW, L"Logs will appear here.\r\n",
        WS_CHILD | WS_VISIBLE | ES_LEFT | ES_MULTILINE | ES_AUTOVSCROLL |
            ES_READONLY | WS_VSCROLL,
        0, 0, 0, 0, windowHandle, reinterpret_cast<HMENU>(LogEdit), NULL, NULL);

    createdState->StatusTextHandle = CreateWindowExW(
        0, WC_STATICW, L"Ready", WS_CHILD | WS_VISIBLE, 0, 0, 0, 0,
        windowHandle, reinterpret_cast<HMENU>(StatusText), NULL, NULL);

    SearchEditOriginalProc = reinterpret_cast<WNDPROC>(
        SetWindowLongPtrW(createdState->SearchEditHandle, GWLP_WNDPROC,
                          reinterpret_cast<LONG_PTR>(SearchEditProc)));

    InitializeSortCombo(createdState->SortComboHandle);

    ListView_SetExtendedListViewStyle(
        createdState->ResultListHandle,
        LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER | LVS_EX_LABELTIP);

    LVCOLUMNW column = {};
    column.mask = LVCF_TEXT | LVCF_WIDTH | LVCF_SUBITEM;

    column.pszText = const_cast<LPWSTR>(L"Path");
    column.cx = 500;
    ListView_InsertColumn(createdState->ResultListHandle, 0, &column);

    column.pszText = const_cast<LPWSTR>(L"Size");
    column.cx = 120;
    ListView_InsertColumn(createdState->ResultListHandle, 1, &column);

    column.pszText = const_cast<LPWSTR>(L"Modified");
    column.cx = 160;
    ListView_InsertColumn(createdState->ResultListHandle, 2, &column);

    column.pszText = const_cast<LPWSTR>(L"Type");
    column.cx = 120;
    ListView_InsertColumn(createdState->ResultListHandle, 3, &column);

    ResizeChildren(windowHandle, *createdState);
    OnLoaded(windowHandle, *createdState);
    return 0;
  }
  case WM_SIZE:
    if (state != NULL) {
      ResizeChildren(windowHandle, *state);
    }
    return 0;
  case WM_COMMAND:
    if (state != NULL && LOWORD(wParam) == SearchButton &&
        HIWORD(wParam) == BN_CLICKED) {
      PerformSearch(windowHandle, *state);
    }
    return 0;
  case WM_NOTIFY:
    if (state != NULL) {
      NMHDR *header = reinterpret_cast<NMHDR *>(lParam);
      if (header->idFrom == static_cast<UINT_PTR>(ResultList) &&
          header->code == NM_DBLCLK) {
        OpenSelectedResult(*state);
      }
    }
    return 0;
  case WM_DESTROY:
    if (state != NULL && state->SearchEditHandle != NULL &&
        SearchEditOriginalProc != NULL) {
      SetWindowLongPtrW(state->SearchEditHandle, GWLP_WNDPROC,
                        reinterpret_cast<LONG_PTR>(SearchEditOriginalProc));
    }
    delete state;
    PostQuitMessage(0);
    return 0;
  default:
    return DefWindowProcW(windowHandle, message, wParam, lParam);
  }
}
} // namespace

int WINAPI wWinMain(HINSTANCE instanceHandle, HINSTANCE, PWSTR,
                    int showCommand) {
  INITCOMMONCONTROLSEX controls = {};
  controls.dwSize = sizeof(controls);
  controls.dwICC = ICC_LISTVIEW_CLASSES | ICC_STANDARD_CLASSES;
  InitCommonControlsEx(&controls);

  WNDCLASSEXW windowClass = {};
  windowClass.cbSize = sizeof(windowClass);
  windowClass.hInstance = instanceHandle;
  windowClass.lpfnWndProc = WindowProc;
  windowClass.lpszClassName = WindowClassName;
  windowClass.hCursor = LoadCursorW(NULL, IDC_ARROW);
  windowClass.hIcon = LoadIconW(NULL, IDI_APPLICATION);
  windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);

  RegisterClassExW(&windowClass);

  HWND windowHandle =
      CreateWindowExW(0, WindowClassName, L"Example.Everything.Win32.VC",
                      WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 1180,
                      760, NULL, NULL, instanceHandle, NULL);

  ShowWindow(windowHandle, showCommand);
  UpdateWindow(windowHandle);

  MSG message = {};
  while (GetMessageW(&message, NULL, 0, 0) > 0) {
    TranslateMessage(&message);
    DispatchMessageW(&message);
  }

  return static_cast<int>(message.wParam);
}
