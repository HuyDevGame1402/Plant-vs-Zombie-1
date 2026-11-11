
using System.Collections;
using UnityEngine;

public class Level1Intro : MonoBehaviour, ILevelIntro
{
    [Header("Intro Camera & Environment")]
    [SerializeField] private CameraIntroController cameraIntroController;
    [SerializeField] private LevelSodRollMover sodRollMover;

    [SerializeField] private float debrisSettleDelay = 0.3f;

    [Header("GUI")]
    [SerializeField] private GameObject guiGameSelect;
    [SerializeField] private GameObject sunGUI;

    [Header("Plant Selection")]
    [SerializeField] private ButtonSelectPlant buttonSelectPlant;

    [SerializeField] private int plantedPeashooterCount;
    private bool hasChosenPlant;
    private int choosePlantCount;

    [Header("Intro Objects")]
    [SerializeField] private GameObject lawnMower;
    [SerializeField] private GameObject imageBgText;
    [SerializeField] private GameObject[] arrayText = new GameObject[11];

    [SerializeField] private LevelSodRollAnimation grassAnimation;
    [SerializeField] private CampaignWaveSpawner waveSpawner;

    public IEnumerator PlayIntro()
    {
        // 1. Chạy camera Intro
        yield return StartCoroutine(
            cameraIntroController.PlayCameraIntro()
        );

        // 2. Chạy hiệu ứng cuộn đất
        if (sodRollMover != null)
        {
            sodRollMover.Play();

            yield return new WaitUntil(
                () => sodRollMover.IsFinished
            );

            if (debrisSettleDelay > 0f)
                yield return new WaitForSeconds(debrisSettleDelay);
        }

        lawnMower.SetActive(true);

        // 3. Hiển thị GUI chọn cây
        guiGameSelect.SetActive(true);
        imageBgText.SetActive(true);
        arrayText[0].SetActive(true);

        // 4. Kiểm tra CreatePlantManager
        CreatePlantManager manager = CreatePlantManager.Instance;

        if (manager == null)
        {
            Debug.LogError("CreatePlantManager chưa được khởi tạo!");
            yield break;
        }

        SunMoneyManager sunManager = SunMoneyManager.Instance;

        if (sunManager == null)
        {
            Debug.LogError("SunMoneyManager chưa được khởi tạo!");
            yield break;
        }

        if (buttonSelectPlant == null)
        {
            Debug.LogError("ButtonSelectPlant chưa được gán!");
            yield break;
        }

        // 5. Reset trạng thái Intro
        plantedPeashooterCount = 0;
        hasChosenPlant = false;
        choosePlantCount = 0;

        // 6. Đăng ký Event
        buttonSelectPlant.OnChoosePlantEvent += HandleChoosePlant;
        manager.OnCreatedPlantEvent += HandlePlantCreated;
        sunManager.OnSunMoneyChanged += HandleSunMoneyChanged;

        try
        {
            // Chờ click chọn cây lần 2 và trồng đủ 2 Peashooter
            yield return new WaitUntil(
                () => plantedPeashooterCount >= 2 && choosePlantCount >= 1
            );
        }
        finally
        {
            buttonSelectPlant.OnChoosePlantEvent -= HandleChoosePlant;
            manager.OnCreatedPlantEvent -= HandlePlantCreated;
            sunManager.OnSunMoneyChanged -= HandleSunMoneyChanged;
        }

        Debug.Log("Đã trồng đủ 2 Peashooter!");
    }

    // =========================================
    // PLANT SELECTION EVENT
    // =========================================

    private void HandleChoosePlant(ButtonSelectPlant button, PlantData plantData)
    {
        if (!hasChosenPlant)
        {
            hasChosenPlant = true;
            OnFirstChoosePlant();
            return;
        }

        if (plantedPeashooterCount >= 1 && choosePlantCount == 0)
        {
            choosePlantCount++;
            OnSecondChoosePlant();
        }
    }

    private void OnFirstChoosePlant()
    {
        Debug.Log("Người chơi đã chọn cây lần đầu!");
        grassAnimation.StartBlinkDark();
        arrayText[0].SetActive(false);
        arrayText[1].SetActive(true);
    }

    private void OnSecondChoosePlant()
    {
        Debug.LogWarning("Người chơi đã click chọn cây sau khi trồng Peashooter thứ hai!");

        // Action khi click chọn cây lần thứ hai
        grassAnimation.StartBlinkDark();
    }

    // =========================================
    // SUN MONEY EVENT
    // =========================================

    private void HandleSunMoneyChanged(int currentSun)
    {
        if (currentSun == 75)
        {
            OnSunMoneyReached75();
        }
        else if (currentSun == 100)
        {
            OnSunMoneyReached100();
        }
    }

    private void OnSunMoneyReached75()
    {
        Debug.Log("Sun đã đạt 75!");

        arrayText[4].SetActive(false);
        arrayText[5].SetActive(true);
        arrayText[6].SetActive(true);
    }

    private void OnSunMoneyReached100()
    {
        Debug.Log("Sun đã đạt 100!");

        arrayText[5].SetActive(false);
        arrayText[6].SetActive(false);
        arrayText[7].SetActive(true);
        arrayText[8].SetActive(true);

        // Khóa tạm thời chọn cây
        StartCoroutine(CoroutineLockButtonSelectPlant());
    }

    private IEnumerator CoroutineLockButtonSelectPlant()
    {
        buttonSelectPlant.SetIsLockedPlant(true);

        yield return new WaitForSeconds(2f);

        buttonSelectPlant.SetIsLockedPlant(false);

        arrayText[7].SetActive(false);
        arrayText[8].SetActive(false);
        arrayText[9].SetActive(true);
    }

    // =========================================
    // CREATE PLANT EVENT
    // =========================================

    private void HandlePlantCreated()
    {
        plantedPeashooterCount++;

        switch (plantedPeashooterCount)
        {
            case 1:
                OnPlantedFirstPeashooter();
                break;

            case 2:
                OnPlantedSecondPeashooter();
                break;

            default:
                OnPlantedAdditionalPeashooter();
                break;
        }
    }

    private void OnPlantedFirstPeashooter()
    {
        Debug.Log("Đã trồng Peashooter đầu tiên!");
        grassAnimation.ResetGrassColor();
        arrayText[1].SetActive(false);
        arrayText[2].SetActive(true);
        arrayText[3].SetActive(true);

        StartCoroutine(CoroutineDelayShowText());
    }

    private IEnumerator CoroutineDelayShowText()
    {
        yield return new WaitForSeconds(2f);

        sunGUI.SetActive(true);

        arrayText[3].SetActive(false);
        arrayText[2].SetActive(false);
        arrayText[4].SetActive(true);
    }

    private void OnPlantedSecondPeashooter()
    {
        Debug.Log("Đã trồng Peashooter thứ hai!");
        grassAnimation.ResetGrassColor();
        arrayText[9].SetActive(false);
        arrayText[10].SetActive(true);
        waveSpawner.StartSpawning();
        StartCoroutine(CoroutineDelayDisableTextGUI());
    }

    private IEnumerator CoroutineDelayDisableTextGUI()
    {
        yield return new WaitForSeconds(3f);
        imageBgText.SetActive(false);
        arrayText[10].SetActive(false);
    }

    private void OnPlantedAdditionalPeashooter()
    {
        Debug.Log("Đã trồng thêm Peashooter!");

        // Action khi trồng từ cây thứ ba trở đi
    }
}
