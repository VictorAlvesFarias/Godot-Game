using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Features.Managers.Save.Resources
{
    // Obsoleto: portal virou prop generico e o dado passou a ser PropSaveData.
    //
    // A classe continua existindo porque os world.tres ja salvos gravaram o CAMINHO deste script
    // dentro do recurso - apagar o arquivo faz o Godot falhar ao carregar esses mundos. Herdando
    // de PropSaveData, o save antigo carrega e o SaveManager.MigrateLegacyPortals move a lista
    // "Portals" para "Props" na leitura. O proximo save ja grava so PropSaveData.
    [SaveType("portal_legacy")]
    public partial class PortalSaveData : PropSaveData
    {
    }
}
