using MetaRemover.Core;
namespace MetaRemover.Tests;
public class CatalogTests
{
    [Fact]
    public void EveryBuiltInPhoneAndCameraProducesAValidCompleteProfile()
    {
        foreach(var model in PresetService.Models)
        {
            var cameras=CameraCatalog.ForModel(model);
            Assert.NotEmpty(cameras);
            foreach(var camera in cameras)
            {
                var settings=AutoPresetService.Create(model,camera.Name);
                Assert.Equal(camera.Name,settings.Camera);
                Assert.NotNull(settings.FocalLength);
                Assert.NotNull(settings.FocalLength35mm);
                Assert.True(settings.UseExportDateWhenMissing);
                Assert.Empty(new PresetService().Validate(settings));
            }
        }
    }
}
