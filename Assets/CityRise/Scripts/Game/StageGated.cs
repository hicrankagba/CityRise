namespace CityRise.Game
{
    /// <summary>Yalnızca belirli aşamalar arasında görünen nesne (yol parçası, sınır çiti, zemin).</summary>
    public class StageGated : UnityEngine.MonoBehaviour
    {
        public Core.CityStage minStage = Core.CityStage.Seyyar;
        public Core.CityStage maxStage = Core.CityStage.Metropol;

        public void Apply(Core.CityStage stage) => gameObject.SetActive(stage >= minStage && stage <= maxStage);
    }
}
