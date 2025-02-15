// This code was contributed by Koustubh Moharir (Thank you!)

#pragma once
#include <string>
#include <OAIdl.h>

class TempFileHolder
{
public:
	~TempFileHolder();
	void setFileName(std::wstring fileName);
	void destroy();
private:
	std::wstring fileName;
};

template <int size>
int CompareNoCase(const std::wstring& str1, wchar_t const (&str2)[size])
{
	return _wcsnicmp(str1.c_str(), str2, size - 1);
}

class SafeVariantArray
{
public:
	explicit SafeVariantArray(int size);
	~SafeVariantArray();
	void lock();
	void unlock();
	void setElement(int i, INT32 value);
	void setElement(int i, INT64 value);
	void setElement(int i, BSTR value);
	operator LPSAFEARRAY() { return pArray; }
private:
	LPSAFEARRAY pArray;
};

class SafeByteArray
{
public:
	SafeByteArray(void* data, int sizeInBytes);
	~SafeByteArray();
	operator LPSAFEARRAY() { return pArray; }
	int AccessData(byte** ppData);
	void UnaccessData();
private:
	LPSAFEARRAY pArray;
};

bool FileExists(LPCTSTR szPath);

// inline bool IsEqualObject(IUnknownPtr ptr1, IUnknownPtr ptr2) { return ptr1 == ptr2; }

std::wstring LoadStringFromResource(HMODULE hModule, int id);

std::wstring FormatString(std::wstring formatString, ...);

void RemoveFileSpecFromPath(std::wstring& filePath);

void StripPath(std::wstring& filePath);

void RemoveExtension(std::wstring& filePath);

void RenameExtension(std::wstring& filePath, std::wstring ext);

HRESULT HResultFromLastError();

class SafeFile
{
public:
	SafeFile();
	~SafeFile();
	HRESULT Create(std::wstring path, DWORD dwDesiredAccess, DWORD dwShareMode, DWORD dwCreationDisposition);
	HRESULT Read(LPVOID pBuffer, DWORD nBufSize, DWORD& nBytesRead);
private:
	HANDLE handle;
};

std::wstring UTF8toUTF16(const std::string& utf8);

void XorRecode(SafeByteArray& data);

HRESULT WriteAllBytes(const std::wstring& filePath, void* buf, DWORD size);

std::wstring PathCombine(const std::wstring& path1, const std::wstring& path2);
std::wstring PathCombine(const std::wstring& path1, const std::wstring& path2, const std::wstring& path3);
