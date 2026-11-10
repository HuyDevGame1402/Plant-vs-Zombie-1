using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntroManager : MonoBehaviour
{
    [SerializeField] private Level1Intro level1Intro;

    public IEnumerator PlayIntro(int level)
    {
        ILevelIntro intro = level switch
        {
            1 => level1Intro,
            _ => null
        };

        if (intro != null)
            yield return intro.PlayIntro();
    }
}