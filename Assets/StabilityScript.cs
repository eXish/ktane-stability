using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class StabilityScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;

    public KMSelectable[] buttons;
    public MeshRenderer[] renderers;
    public MeshRenderer[] ledRenderers;
    public Material[] materials;
    public TextMesh[] displays;
    public GameObject[] box;
    public GameObject hider;

    private List<int> correctSquares = new List<int>();
    private List<int> correctSquaresState = new List<int>();
    private List<int> correctSquaresLED = new List<int>();
    private Coroutine[] coroutines = new Coroutine[2];
    private int[][] table1 = { new int[] { 8, 7, 32, 17, 18, 5, 17, 1, 27, 30 }, 
                               new int[] { 18, 29, 20, 0, 32, 31, 27, 23, 15, 10 },
                               new int[] { 32, 0, 9, 17, 25, 20, 6, 24, 32, 26 } };
    private int[][] table2 = { new int[] { 29, 22, 29, 8, 10, 1, 11, 14, 11, 13 },
                               new int[] { 34, 9, 19, 23, 31, 3, 11, 8, 25, 13 },
                               new int[] { 14, 24, 3, 4, 30, 30, 14, 22, 15, 28 } };
    private int[][] table3 = { new int[] { 15, 12, 23, 33, 30, 0, 15, 34, 6, 13 },
                               new int[] { 32, 27, 1, 5, 32, 30, 7, 26, 17, 34 },
                               new int[] { 12, 20, 22, 31, 5, 2, 21, 8, 34, 19 } };
    private int[][] table4 = { new int[] { 22, 1, 14, 14, 35, 24, 33, 33, 23, 23 },
                               new int[] { 0, 3, 24, 0, 29, 32, 8, 35, 19, 16 },
                               new int[] { 32, 25, 0, 15, 29, 4, 4, 32, 6, 1 } };
    private int[][] table1States = { new int[] { 1, 1, 0, 0, 1, 1, 1, 0, 1, 0 },
                                     new int[] { 1, 0, 1, 0, 0, 0, 1, 0, 1, 1 },
                                     new int[] { 0, 1, 1, 1, 1, 1, 0, 0, 1, 0 } };
    private int[][] table2States = { new int[] { 1, 0, 1, 1, 1, 1, 0, 1, 0, 1 },
                                     new int[] { 0, 1, 0, 1, 1, 1, 1, 0, 1, 1 },
                                     new int[] { 0, 0, 1, 1, 1, 0, 1, 1, 1, 1 } };
    private int[][] table3States = { new int[] { 0, 1, 1, 1, 0, 0, 0, 1, 0, 1 },
                                     new int[] { 1, 0, 1, 0, 0, 1, 0, 1, 0, 1 },
                                     new int[] { 0, 0, 0, 1, 0, 1, 0, 1, 1, 0 } };
    private int[][] table4States = { new int[] { 0, 0, 0, 1, 0, 0, 1, 0, 0, 0 },
                                     new int[] { 1, 0, 1, 0, 1, 1, 0, 1, 0, 0 },
                                     new int[] { 0, 1, 1, 1, 1, 0, 0, 0, 0, 1} };
    private string[] coords = { "A1", "A2", "A3", "A4", "A5", "A6", "B1", "B2", "B3", "B4", "B5", "B6", "C1", "C2", "C3", "C4", "C5", "C6", "D1", "D2", "D3", "D4", "D5", "D6", "E1", "E2", "E3", "E4", "E5", "E6", "F1", "F2", "F3", "F4", "F5", "F6" };
    private string[] colorNames = { "Red", "Yellow", "Blue", "Off" };
    private string[] states = new string[36];
    private int[] ledStates = new int[6];
    private string idNumber;
    private int currentState;
    private int currentSquare;
    private float naturalNum;
    private float currentNum;
    private float lastFlicker;
    private float curTime;
    private bool activated;
    private bool animating;
    private bool flicker;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void Start () {
        foreach (GameObject obj in box)
            obj.SetActive(false);
        for (int i = 0; i < 3; i++)
            displays[i].text = "";
        for (int i = 0; i < 36; i++)
        {
            redo:
            states[i] = "";
            for (int j = 0; j < 6; j++)
                states[i] += UnityEngine.Random.Range(0, 2);
            if (states[i].Count(f => f == '0') != 3)
                goto redo;
        }
        currentState = UnityEngine.Random.Range(0, 6);
        naturalNum = UnityEngine.Random.Range(2f, 9.9f);
        naturalNum = (float)Math.Round(naturalNum * 100f) / 100f;
        currentNum = naturalNum;
        Debug.LogFormat("[Stability #{0}] Natural Stability: {1}", moduleId, naturalNum.ToString("0.00"));
        GenerateCorrectSquares(false);
    }

    void OnActivate()
    {
        coroutines[1] = StartCoroutine(CyclePixels());
        displays[1].text = naturalNum.ToString("0.00");
        displays[0].text = currentNum.ToString("0.00");
        coroutines[0] = StartCoroutine(StabilityChanger());
        for (int i = 0; i < 6; i++)
            ledRenderers[i].material = materials[ledStates[i]];
        displays[2].text = idNumber;
        foreach (GameObject obj in box)
            obj.SetActive(true);
        activated = true;
    }

    void GenerateCorrectSquares(bool regenIdOnly)
    {
        idNumber = "";
        for (int i = 0; i < 4; i++)
            idNumber += UnityEngine.Random.Range(0, 10);
        correctSquares.Clear();
        correctSquaresState.Clear();
        if (!regenIdOnly)
        {
            List<int> temp = new List<int>{ 5, 5, 5, 5 };
            int numOfLeds = UnityEngine.Random.Range(0, 5);
            for (int i = 0; i < numOfLeds; i++)
            {
                temp[i] = UnityEngine.Random.Range(0, 3);
            }
            for (int i = 0; i < 2; i++)
                temp.Add(UnityEngine.Random.Range(0, 3));
            temp = temp.Shuffle();
            string logStrips = "";
            for (int i = 0; i < 6; i++)
            {
                ledStates[i] = temp[i];
                if (i == 5)
                {
                    if (ledStates[i] == 5)
                        logStrips += colorNames[3];
                    else
                        logStrips += colorNames[ledStates[i]];
                }
                else
                {
                    if (ledStates[i] == 5)
                        logStrips += colorNames[3] + ", ";
                    else
                        logStrips += colorNames[ledStates[i]] + ", ";
                }
                if (ledStates[i] != 5)
                    correctSquaresLED.Add(i);
            }
            Debug.LogFormat("[Stability #{0}] LED Strips: {1}", moduleId, logStrips);
        }
        int ct = 0;
        int sum = 0;
        for (int i = 0; i < 4; i++)
            sum += int.Parse(idNumber[i].ToString());
        for (int i = 0; i < 6; i++)
        {
            if (ledStates[i] != 5)
            {
                switch (ct)
                {
                    case 0:
                        correctSquares.Add(table1[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        correctSquaresState.Add(table1States[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        break;
                    case 1:
                        correctSquares.Add(table2[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        correctSquaresState.Add(table2States[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        break;
                    case 2:
                        correctSquares.Add(table3[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        correctSquaresState.Add(table3States[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        break;
                    case 3:
                        correctSquares.Add(table4[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        correctSquaresState.Add(table4States[ledStates[i]][int.Parse(idNumber[ct].ToString())]);
                        break;
                    case 4:
                        if (sum > 0)
                            correctSquares.Add(sum - 1);
                        else
                            correctSquares.Add(-1);
                        if (ledStates[i] == 2)
                            correctSquaresState.Add(0);
                        else
                            correctSquaresState.Add(1);
                        break;
                    case 5:
                        if ((36 - sum) > 0)
                            correctSquares.Add((36 - sum) - 1);
                        else
                            correctSquares.Add(-1);
                        if (ledStates[i] == 2 || ledStates[i] == 1)
                            correctSquaresState.Add(0);
                        else
                            correctSquaresState.Add(1);
                        break;
                }
                ct++;
            }
        }
        if (!regenIdOnly)
            Debug.LogFormat("[Stability #{0}] Identification Number: {1}", moduleId, idNumber);
        else
        {
            displays[2].text = idNumber;
            Debug.LogFormat("[Stability #{0}] New Identification Number: {1}", moduleId, idNumber);
        }
        string log = "";
        for (int i = currentSquare; i < correctSquares.Count; i++)
        {
            if (i == (correctSquares.Count - 1))
            {
                if (correctSquares[i] == -1)
                    log += "Any " + (correctSquaresState[i] == 0 ? "(off)" : "(on)");
                else
                    log += coords[correctSquares[i]] + (correctSquaresState[i] == 0 ? " (off)" : " (on)");
            }
            else
            {
                if (correctSquares[i] == -1)
                    log += "Any " + (correctSquaresState[i] == 0 ? "(off), " : "(on), ");
                else
                    log += coords[correctSquares[i]] + (correctSquaresState[i] == 0 ? " (off), " : " (on), ");
            }
        }
        if (!regenIdOnly)
            Debug.LogFormat("[Stability #{0}] Correct Squares: {1}", moduleId, log);
        else
            Debug.LogFormat("[Stability #{0}] New Correct Squares: {1}", moduleId, log);
    }

    void PressButton(KMSelectable pressed)
    {
        if (moduleSolved != true && activated != false && animating != true)
        {
            pressed.AddInteractionPunch();
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
            audio.PlaySoundAtTransform("beep"+UnityEngine.Random.Range(1, 5), pressed.transform);
            int index = Array.IndexOf(buttons, pressed);
            Debug.LogFormat("[Stability #{0}] Pressed square {1} ({2}) when the current stability was {3}", moduleId, coords[index], states[index][currentState] == '0' ? "off" : "on", currentNum.ToString("0.00"));
            float limit = naturalNum + 0.04f;
            limit = (float)Math.Round(limit * 100f) / 100f;
            if (currentNum >= limit)
            {
                GetComponent<KMBombModule>().HandleStrike();
                Debug.LogFormat("[Stability #{0}] The current stability is at least 0.04 over the natural stability. Strike! Resetting identification number...", moduleId);
                GenerateCorrectSquares(true);
            }
            else if (((correctSquares[currentSquare] == index) || (correctSquares[currentSquare] == -1)) && (correctSquaresState[currentSquare] == int.Parse(states[index][currentState].ToString())))
            {
                ledRenderers[correctSquaresLED[currentSquare]].material = materials[6];
                if (correctSquaresLED[currentSquare] == correctSquaresLED.Last())
                {
                    animating = true;
                    StopCoroutine(coroutines[0]);
                    StartCoroutine(StabilizeAnim());
                }
                currentSquare++;
            }
            else
            {
                GetComponent<KMBombModule>().HandleStrike();
                Debug.LogFormat("[Stability #{0}] That square is incorrect. Strike! Resetting identification number...", moduleId);
                GenerateCorrectSquares(true);
            }
        }
    }

    IEnumerator CyclePixels()
    {
        while (true)
        {
            for (int i = 0; i < 36; i++)
            {
                if (states[i][currentState] == '0')
                    renderers[i].material = materials[3];
                else
                    renderers[i].material = materials[4];
            }
            float t = 0;
            while (t < 1f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentState++;
            if (currentState == 6)
                currentState = 0;
        }
    }

    IEnumerator StabilityChanger()
    {
        while (true)
        {
            curTime = 0f;
            lastFlicker = 0f;
            int choice = UnityEngine.Random.Range(4, 9);
            while (curTime < choice)
            {
                yield return null;
                curTime += Time.deltaTime;
                int choice2 = UnityEngine.Random.Range(0, 200);
                float choice3 = UnityEngine.Random.Range(.2f, .4f);
                if (choice2 == 0 && !flicker && curTime < (choice - choice3 - 0.5f) && curTime > (lastFlicker - 0.5f) && curTime > 0.5f)
                {
                    flicker = true;
                    StartCoroutine(FlickerStability(choice3));
                }
            }
            currentNum += 0.03f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
            float t = 0;
            while (t < 0.4f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentNum += 0.02f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
            for (int i = 0; i < 3; i++)
                displays[i].color = new Color32(255, 0, 0, 255);
            for (int i = 5; i < 8; i++)
                displays[i].color = new Color32(58, 24, 29, 255);
            foreach (GameObject obj in box)
                obj.GetComponent<MeshRenderer>().material = materials[7];
            t = 0;
            while (t < 0.7f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentNum += 0.02f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
            curTime = 0f;
            lastFlicker = 0f;
            choice = UnityEngine.Random.Range(3, 7);
            while (curTime < choice)
            {
                yield return null;
                curTime += Time.deltaTime;
                int choice2 = UnityEngine.Random.Range(0, 100);
                float choice3 = UnityEngine.Random.Range(.1f, .3f);
                if (choice2 == 0 && !flicker && curTime < (choice - choice3 - 0.3f) && curTime > (lastFlicker - 0.3f) && curTime > 0.3f)
                {
                    flicker = true;
                    StartCoroutine(FlickerStability(choice3));
                }
            }
            currentNum -= 0.02f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
            t = 0;
            while (t < 0.7f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentNum -= 0.02f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
            for (int i = 0; i < 3; i++)
                displays[i].color = new Color32(0, 255, 33, 255);
            for (int i = 5; i < 8; i++)
                displays[i].color = new Color32(24, 58, 29, 255);
            foreach (GameObject obj in box)
                obj.GetComponent<MeshRenderer>().material = materials[6];
            t = 0;
            while (t < 0.4f)
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentNum -= 0.03f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
        }
    }

    IEnumerator FlickerStability(float dur)
    {
        int choice = UnityEngine.Random.Range(0, 2);
        if (choice == 0)
            currentNum += 0.01f;
        else
            currentNum -= 0.01f;
        currentNum = (float)Math.Round(currentNum * 100f) / 100f;
        displays[0].text = currentNum.ToString("0.00");
        float t = 0;
        while (t < dur)
        {
            yield return null;
            t += Time.deltaTime;
        }
        if (choice == 0)
            currentNum -= 0.01f;
        else
            currentNum += 0.01f;
        currentNum = (float)Math.Round(currentNum * 100f) / 100f;
        displays[0].text = currentNum.ToString("0.00");
        lastFlicker = curTime;
        flicker = false;
    }

    IEnumerator StabilizeAnim()
    {
        while (currentNum != 0f)
        {
            float t = 0;
            while (t < (0.01f - ((naturalNum - 2f) / 800)))
            {
                yield return null;
                t += Time.deltaTime;
            }
            currentNum -= 0.01f;
            currentNum = (float)Math.Round(currentNum * 100f) / 100f;
            displays[0].text = currentNum.ToString("0.00");
        }
        moduleSolved = true;
        StopCoroutine(coroutines[1]);
        hider.SetActive(false);
        displays[3].text = "STABILIZED";
        displays[4].text = "8888888888";
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        for (int i = 0; i < 6; i++)
            ledRenderers[i].material = materials[5];
        for (int i = 0; i < 36; i++)
            renderers[i].material = materials[3];
        GetComponent<KMBombModule>().HandlePass();
        Debug.LogFormat("[Stability #{0}] All correct squares have been pressed. Module stabilized!", moduleId);
        animating = false;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <coord> <on/off> [Presses the square at the specified coord when it is on or off and the explosives are not unstable]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length > 3)
            {
                yield return "sendtochaterror Too many parameters!";
            }
            else if (parameters.Length == 3)
            {
                if (!coords.Contains(parameters[1].ToUpper()))
                {
                    yield return "sendtochaterror!f The specified coord '"+parameters[1]+"' is invalid!";
                    yield break;
                }
                if (!parameters[2].ToUpper().EqualsAny("ON", "OFF"))
                {
                    yield return "sendtochaterror!f The specified state '" + parameters[1] + "' is invalid!";
                    yield break;
                }
                if (animating)
                    yield break;
                int state = 0;
                if (parameters[2].ToUpper().Equals("ON"))
                    state = 1;
                float limit = naturalNum + 0.04f;
                limit = (float)Math.Round(limit * 100f) / 100f;
                while ((state != int.Parse(states[Array.IndexOf(coords, parameters[1].ToUpper())][currentState].ToString())) || (currentNum >= limit))
                    yield return "trycancel Halted waiting to press a square due to a cancel request";
                buttons[Array.IndexOf(coords, parameters[1].ToUpper())].OnInteract();
                if (animating)
                    yield return "solve";
            }
            else if (parameters.Length == 2)
            {
                yield return "sendtochaterror Please specify a state for the square!";
            }
            else if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify a coord and a state of a square!";
            }
            yield break;
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        int start = currentSquare;
        for (int i = start; i < correctSquaresState.Count; i++)
        {
            float limit = naturalNum + 0.04f;
            limit = (float)Math.Round(limit * 100f) / 100f;
            while ((correctSquaresState[i] != int.Parse(states[correctSquares[i]][currentState].ToString())) || (currentNum >= limit)) yield return true;
            buttons[correctSquares[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (!moduleSolved) yield return true;
    }
}
