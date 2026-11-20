using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Pancras.Web.Catalog;

namespace Pancras.Web.Tests;

public class CatalogImageStoreTests
{
    private sealed class Environment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = root;

        public string EnvironmentName { get; set; } = "Testing";

        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IFormFile Png(string name = "a.png")
    {
        // Chữ ký PNG tối thiểu đủ để vượt kiểm tra nội dung.
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", name) { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }

    [Fact]
    public async Task ConfiguredUploadsPath_ReplacesTheWebRootFolder()
    {
        var content = Path.Combine(Path.GetTempPath(), "pancras-img-" + Guid.NewGuid().ToString("N"));
        var uploads = Path.Combine(content, "outside-uploads");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:UploadsPath"] = uploads }).Build();
            var store = new CatalogImageStore(new Environment(content), configuration);

            var draft = await store.ReadAsync(Png(), CancellationToken.None);
            Assert.True(draft.Accepted);
            await store.SaveAsync(draft, CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(uploads, "catalog")));
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(uploads, "catalog")));
            Assert.False(Directory.Exists(Path.Combine(content, "wwwroot", "uploads")));
        }
        finally
        {
            if (Directory.Exists(content))
            {
                Directory.Delete(content, true);
            }
        }
    }
}
