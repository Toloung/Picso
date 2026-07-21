using LocalPhotoManager.Core.Services;

namespace LocalPhotoManager.Core.Tests;

public sealed class PathPolicyTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("PHOTO.JPEG")]
    [InlineData("image.webp")]
    public void IsSupportedImageReturnsTrueForSupportedExtensions(string path)
    {
        Assert.True(PathPolicy.IsSupportedImage(path));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("image.raw")]
    [InlineData("no-extension")]
    public void IsSupportedImageReturnsFalseForUnsupportedExtensions(string path)
    {
        Assert.False(PathPolicy.IsSupportedImage(path));
    }

    [Fact]
    public void IsExcludedDirectoryRecognizesRepositoryArtifacts()
    {
        Assert.True(PathPolicy.IsExcludedDirectory(Path.Combine("C:", "work", "node_modules")));
        Assert.False(PathPolicy.IsExcludedDirectory(Path.Combine("C:", "pictures", "2026")));
    }
}
