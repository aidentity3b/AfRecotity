﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using System;
using SFB;

public class RecordingManager : MonoBehaviour
{
    [SerializeField]
    private VideoPlayer vp;
    [SerializeField]
    private Text TimeText;
    [SerializeField]
    private Slider SeekBar;
    [SerializeField]
    private Image MicLevel;
    [SerializeField]
    private Image BG;
    [SerializeField]
    AlertManager alertManager;
    [SerializeField]
    Button RecordButton;
    [SerializeField]
    private AudioSource Recorder;
    [SerializeField]
    private AudioSource Player;
    [SerializeField]
    InputField videoUrl;
    [SerializeField]
    Button[] ControlButtons;
    [SerializeField]
    ReviewingManager reviewer;

    bool focusOnSeekBar = false;
    float lengthSec;
    int recordOffset = 0;
    double startTime = 0;

    private AudioClip TemporarySound;
    private AudioClip MasterSound;
    private bool seekChangingByPlaying = false;
    private enum PlayingStateType
    {
        None, Playing, Recording, Pausing, Seeking
    }
    private PlayingStateType PlayingState = PlayingStateType.None;
    private bool atLoading = false;

    // Use this for initialization
    void Start()
    {
        foreach (Button b in ControlButtons)
        {
            b.interactable = false;
        }
        ObservableEventTrigger et = SeekBar.gameObject.AddComponent<ObservableEventTrigger>();
        et.OnPointerDownAsObservable().Subscribe(x => { focusOnSeekBar = true; });
        et.OnPointerUpAsObservable().Subscribe(x => { focusOnSeekBar = false; });
        SeekBar.OnValueChangedAsObservable().Where(x => !seekChangingByPlaying).Throttle(new System.TimeSpan(1000))
            .Subscribe(x =>
        {
            if (vp.canSetTime)
            {
                vp.time = lengthSec * x;
            }

        });
        vp.seekCompleted += OnSeekCompleted;
        vp.prepareCompleted += LoadClip;
    }

    private void OnSeekCompleted(VideoPlayer source)
    {
        Player.time = (float)vp.time;
    }

    // Update is called once per frame
    void Update()
    {
        if (PlayingState == PlayingStateType.Playing || PlayingState == PlayingStateType.Recording || PlayingState == PlayingStateType.Pausing)
        {
            float rawTime = (float)vp.time;
            float remainTime = rawTime;
            int minute = Mathf.FloorToInt(remainTime / 60f);
            remainTime -= minute * 60;
            int second = Mathf.FloorToInt(remainTime);
            remainTime -= second;
            float millisecond = Mathf.FloorToInt(remainTime * 1000);
            string recIndicator = PlayingState == PlayingStateType.Recording ? "<color=#c00>●</color>" : "";
            string builtTimeText = $"{recIndicator}{minute.ToString()}:{second.ToString()}:{millisecond.ToString()}";
            TimeText.text = builtTimeText;
            if ((PlayingState == PlayingStateType.Playing || PlayingState == PlayingStateType.Recording) && !focusOnSeekBar)
            {
                seekChangingByPlaying = true;
                SeekBar.value = (float)(vp.time / lengthSec);
                seekChangingByPlaying = false;
            }
        }
        else if (PlayingState == PlayingStateType.None)
        {
            string builtTimeText = $"-:-:-";
            TimeText.text = builtTimeText;
        }
        float[] data = new float[64];
        float a = 0;
        Recorder.GetOutputData(data, 0);
        foreach (float s in data)
        {
            a += Mathf.Abs(s);
        }
        MicLevel.rectTransform.sizeDelta = new Vector2(a * 40, 50);
    }
    private void FixedUpdate()
    {
        if (Microphone.IsRecording(Microphone.devices[0]))
        {
            if (Recorder.timeSamples - Microphone.GetPosition(Microphone.devices[0]) > 500)
            {
                //fix position
                Debug.Log(Math.Abs(Recorder.timeSamples - Microphone.GetPosition(Microphone.devices[0])));
                int pos = Microphone.GetPosition(Microphone.devices[0]);
                if (pos >= 300)
                {
                    Recorder.timeSamples = Microphone.GetPosition(Microphone.devices[0]) - 300;
                }
            }
        }
    }
    public void Play()
    {
        if (PlayingState == PlayingStateType.Recording) StopRecording();
        RecordButton.interactable = false;
        vp.Play();
        Player.Play();
        PlayingState = PlayingStateType.Playing;
    }
    public void Pause()
    {
        if (PlayingState == PlayingStateType.Recording) StopRecording();
        RecordButton.interactable = true;
        vp.Pause();
        Player.Pause();
        PlayingState = PlayingStateType.Pausing;
    }

    public void Stop()
    {
        if (PlayingState == PlayingStateType.Recording) StopRecording();
        RecordButton.interactable = false;
        vp.Stop();
        Player.Stop();
        PlayingState = PlayingStateType.None;
    }
    public void PickVideoFile()
    {
        StandaloneFileBrowser.OpenFilePanelAsync("ビデオを開く", "", "", false,
            x =>
           {
               Debug.Log("CALLBACK");
               if (x.Length == 1)
               {
                   Debug.Log(x[0]);
                   videoUrl.text = x[0];
                   LoadClip(x[0]);
               }
           });

    }
    public void LoadClip(InputField clip)
    {
        LoadClip(clip.text);
    }
    public void LoadClip(string clip)
    {
        Stop();
        vp.url = clip;
        atLoading = true;
        vp.Prepare();
    }
    public void LoadClip(VideoClip clip)
    {
        Stop();
        vp.clip = clip;
        atLoading = true;
        vp.Prepare();
    }
    public void LoadClip(VideoPlayer source)
    {
        if (!atLoading) return;
        atLoading = false;
        //if (!vp.clip) return;
        lengthSec = vp.frameCount / vp.frameRate;
        foreach (Button b in ControlButtons)
        {
            b.interactable = true;
        }
        Recorder.Stop();
        Microphone.End(Microphone.devices[0]);
        TemporarySound = Microphone.Start(Microphone.devices[0], true, Mathf.CeilToInt(lengthSec), 44100);
        Recorder.clip = TemporarySound;
        Recorder.Play();
        MasterSound = AudioClip.Create("master", 44100 * Mathf.CeilToInt(lengthSec), TemporarySound.channels, 44100, false);
        Player.clip = MasterSound;
    }
    public void StartRecording()
    {
        if (!(PlayingState == PlayingStateType.Pausing)) return;
        Recorder.Stop();
        Microphone.End(Microphone.devices[0]);
        TemporarySound = Microphone.Start(Microphone.devices[0], false, Mathf.CeilToInt(lengthSec), 44100);
        Recorder.clip = TemporarySound;
        Recorder.Play();
        vp.Play();
        startTime = vp.time;
        PlayingState = PlayingStateType.Recording;
        BG.color = new Color(1, 0.4f, 0.5f);
    }
    public void StopRecording()
    {
        if (!(PlayingState == PlayingStateType.Recording)) return;
        int endPos = Microphone.GetPosition(Microphone.devices[0]);
        Microphone.End(Microphone.devices[0]);
        Recorder.Stop();
        vp.Pause();
        Player.Pause();
        PlayingState = PlayingStateType.Pausing;
        Recorder.mute = false;
        reviewer.StartReviewing(this, vp, Recorder, MasterSound, startTime, endPos, Player);

        double length = vp.time - startTime;

        //copy recorded sound from temp to master
        /*
        float[] rec = new float[endPos * TemporarySound.channels];
        TemporarySound.GetData(rec, 0);
        int startPos = MasterSound.channels * Mathf.FloorToInt(MasterSound.samples * ((float)startTime / MasterSound.length));
        MasterSound.SetData(rec, startPos);
        Player.clip = MasterSound;
        Recorder.Stop();
        Microphone.End(Microphone.devices[0]);
        TemporarySound = Microphone.Start(Microphone.devices[0], true, Mathf.CeilToInt((float)vp.clip.length), 44100);
        Recorder.clip = TemporarySound;
        Recorder.Play();
        Player.Pause();*/
        BG.color = new Color(0, 0, 0);
    }

    public void ReturnFromReviewing(AudioClip master)
    {
        //Set written clip to master
        MasterSound = master;
        Player.clip = MasterSound;
        ReturnFromReviewing();
    }
    public void ReturnFromReviewing()
    {
        //restart recording reflection
        TemporarySound = Microphone.Start(Microphone.devices[0], true, Mathf.CeilToInt(lengthSec), 44100);
        Recorder.clip = TemporarySound;
        Recorder.Play();
    }

    public void ToggleRecordingState()
    {
        if (PlayingState == PlayingStateType.Recording)
        {
            StopRecording();
        }
        else if (PlayingState == PlayingStateType.Pausing)
        {
            StartRecording();

        }
    }

    public void Save()
    {
        string path = System.IO.Path.GetDirectoryName(vp.url) + "\\" + System.IO.Path.GetFileNameWithoutExtension(vp.url) + ".wav";
        if (System.IO.File.Exists(path))
        {
            path = $"{System.IO.Path.GetDirectoryName(vp.url)}\\{System.IO.Path.GetFileNameWithoutExtension(vp.url)}_{DateTime.Now.Year.ToString()}{DateTime.Now.Month.ToString()}{DateTime.Now.Day.ToString()}{DateTime.Now.Hour.ToString()}{DateTime.Now.Minute.ToString()}{DateTime.Now.Second.ToString()}.wav";
        }
        string savedPath;
        WavUtility.FromAudioClip(MasterSound, out savedPath);
        try
        {
            System.IO.File.Move(savedPath, path);
        }
        catch (System.IO.IOException e)
        {
            alertManager.NewAlert("An error has occured: " + e.Message);
        }
        alertManager.NewAlert("Saved to: " + path);
        System.Diagnostics.Process.Start(
    "EXPLORER.EXE", "/select,\"" + path + "\"");
    }

}
