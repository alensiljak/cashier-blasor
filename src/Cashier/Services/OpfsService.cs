/*
 * OPFS Helper
 */

using KristofferStrube.Blazor.FileSystem;

namespace Cashier.Services
{
    public class OpfsService
    {
        private IStorageManagerService _storageManagerService;

        public OpfsService(IStorageManagerService storageManagerService)
        {
            _storageManagerService = storageManagerService;
        }

        public async Task DeleteFile(string fileName)
        {
            var dir = await _storageManagerService.GetOriginPrivateDirectoryAsync();
            // var options = new FileSystemRemoveOptions
            await dir.RemoveEntryAsync(fileName);
        }

        public async Task<KristofferStrube.Blazor.FileAPI.File?> OpenFile(string fileName, bool create = true)
        {
            FileSystemDirectoryHandle directoryHandle = await _storageManagerService.GetOriginPrivateDirectoryAsync();
            FileSystemFileHandle fileHandle = await directoryHandle.GetFileHandleAsync(fileName, new() { Create = create });

            if (fileHandle == null)
            {
                return null;
            }

            var file = await fileHandle.GetFileAsync();

            return file;
        }

        public async Task<FileSystemWritableFileStream> OpenWritable(string fileName, bool create = true)
        {
            FileSystemDirectoryHandle directoryHandle = await _storageManagerService.GetOriginPrivateDirectoryAsync();
            FileSystemFileHandle fileHandle = await directoryHandle.GetFileHandleAsync(fileName, new() { Create = create });

            var stream = await fileHandle.CreateWritableAsync();

            return stream;
        }

        public async Task<string> ReadFromFile(string fileName)
        {
            var file = await OpenFile(fileName);
            if (file == null)
            {
                throw new IOException("File not found!");
            }

            var content = await file.TextAsync();
            return content;
        }

        public async Task SaveFile(string fileName, string content)
        {
            using var stream = await OpenWritable(fileName, true);
            await stream.WriteAsync(content);
            await stream.CloseAsync();
        }
    }
}
