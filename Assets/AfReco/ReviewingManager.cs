using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UniRx;
using UniRx.Triggers;
using System;

public class ReviewingManager : MonoBehaviour {
    enum PlayingStareType
    {
        Playing, Stopped
    }
    PlayingStareType PlayingState = PlayingStareType.Playing;
    [SerializeField]
    Slider SeekBar;
    [SerializeField]
    GameObject ReviewPanel;
    [SerializeField]
    AlertManager alertManager;
    [SerializeField]
    Slider AlignmentSlider;
    [SerializeField]
    Text AlignmentText;
    [SerializeField]
    Toggle OverwriteToggle;
         
    VideoPlayer vp;
    AudioSource Player;
    AudioSource Recorder;
    AudioClip Master;
    double StartTime;
    double InitialStartTime;
    int EndPosInRecording;
    double LengthTime;
    double EndTime;
    double InitialEndTime;
    double lastPlayedTime = 0;
    int StartPosInMaster = 0;
    bool focusOnSeekBar = false;
    bool seekingByPlaying = false;
    RecordingManager Sender;
    bool isRunning = false;
    bool isFirstTime = true;

    // Use this for initialization
    void Start () {
        AlignmentSlider.OnValueChangedAsObservable().Subscribe(value =>
        {
            //Recalculate Parameters
            if (isRunning)
            {
                AlignmentText.text = ((int)(value * 1000) / 1000f).ToString();
                StartTime = InitialStartTime + value;
                EndTime = InitialEndTime + value;
                vp.time = StartTime + SeekBar.value * LengthTime;
                vp.Pause();
                Recorder.Pause();
            }
        });

    }
	
	// Update is called once per frame
	void Update () {
        if (!isRunning) return;

        if(Recorder.time > LengthTime)
        {
            double time = StartTime;
            vp.time = time;
            Recorder.time = 0;
            Recorder.Play();
            vp.Play();
        }
        if (!focusOnSeekBar)
        {
            seekingByPlaying = true;
            SeekBar.value = (float)(Recorder.time / LengthTime);
            seekingByPlaying = false;
        }
    }
    public void StartReviewing(RecordingManager sender, VideoPlayer videoPlayer, AudioSource recorder, AudioClip master, double startTime, int endPos, AudioSource player)
    {
        ReviewPanel.SetActive(true);
        Sender = sender;
        vp = videoPlayer;
        Recorder = recorder;
        Player = player;
        StartTime = startTime;
        InitialStartTime = startTime;
        Master = master;
        EndPosInRecording = endPos;
        lastPlayedTime = startTime;
        int samples = Recorder.clip.frequency;
        LengthTime = (float)endPos / samples;
        EndTime = StartTime + LengthTime;
        InitialEndTime = EndTime;
        StartPosInMaster = Master.channels * Mathf.FloorToInt(Master.samples * ((float)startTime / Master.length));
        vp.time = startTime;
        Player.time = (float)startTime;
        Player.Pause();
        vp.Pause();
        Recorder.Pause();
        Player.volume = OverwriteToggle.isOn ? 0 : 1;
        if (isFirstTime)
        {
            ObservableEventTrigger et = SeekBar.gameObject.AddComponent<ObservableEventTrigger>();
            et.OnPointerDownAsObservable().Subscribe(x => { focusOnSeekBar = true; });
            et.OnPointerUpAsObservable().Subscribe(x => { focusOnSeekBar = false; });

            SeekBar.OnValueChangedAsObservable().Where(x => !seekingByPlaying && isRunning).Throttle(new System.TimeSpan(500)).Subscribe(x =>
            {
                double time = StartTime + x * LengthTime;
                lastPlayedTime = time;
                vp.time = time;
                Player.time = (float)time;
                Recorder.time = (float)(x * LengthTime);
                Player.Pause();
                vp.Pause();
                Recorder.Pause();
            });
            OverwriteToggle.OnValueChangedAsObservable().Subscribe(overwrite =>
            {
                if (isRunning)
                {
                    Player.volume = overwrite ? 0 : 1;
                }
            });
            vp.seekCompleted += OnSeekCompleted;
        }
        PlayingState = PlayingStareType.Playing;
        isFirstTime = false;
        isRunning = true;
    }

    private void OnSeekCompleted(VideoPlayer source)
    {
        if (!isRunning) return;
        if (PlayingState == PlayingStareType.Playing)
        {
            Recorder.Play();
            vp.Play();
            Player.Play();
        }
        Recorder.time = (float)(vp.time - StartTime);
        Player.time = (float)vp.time;
    }

    public void Play()
    {
        PlayingState = PlayingStareType.Playing;
        double time = lastPlayedTime;
        vp.time = time;
        Player.time = (float)time;
        Recorder.time = (float)(SeekBar.value * LengthTime);
        Player.Pause();
        vp.Pause();
        Recorder.Pause();
    }
    public void Stop()
    {
        PlayingState = PlayingStareType.Stopped;
        Recorder.time = (float)(lastPlayedTime - StartTime);
        Player.Pause();
        vp.Pause();
        Recorder.Pause();
    }
    public void WriteToMaster()
    {
        isRunning = false;
        StartPosInMaster = Master.channels * Mathf.FloorToInt(Master.samples * ((float)StartTime / Master.length));
        if (StartPosInMaster < 0) StartPosInMaster = 0;
        float[] rec = new float[EndPosInRecording * Recorder.clip.channels];
        Recorder.clip.GetData(rec, 0);
        if (OverwriteToggle.isOn)
        {
            Master.SetData(rec, StartPosInMaster);
        }
        else
        {
            float[] recMaster = new float[EndPosInRecording * Recorder.clip.channels];
            Master.GetData(recMaster, StartPosInMaster);
            for(int i = 0; i < recMaster.Length; i++)
            {
                recMaster[i] += rec[i];
            }
            Master.SetData(recMaster, StartPosInMaster);
        }
        Player.volume = 1;
        ReviewPanel.SetActive(false);
        Sender.ReturnFromReviewing(Master);
    }

    public void SaveIndependently()
    {
        Debug.Log(vp.url);
        string path = $"{System.IO.Path.GetDirectoryName(vp.url)}\\{System.IO.Path.GetFileNameWithoutExtension(vp.url)}_segment_{DateTime.Now.Year.ToString()}{DateTime.Now.Month.ToString()}{DateTime.Now.Day.ToString()}{DateTime.Now.Hour.ToString()}{DateTime.Now.Minute.ToString()}{DateTime.Now.Second.ToString()}.wav";
        string savedPath;
        WavUtility.FromAudioClip(Recorder.clip, out savedPath,true,"recordings",LengthTime);
        try
        {
            System.IO.File.Move(savedPath, path);
        }catch (System.IO.IOException e)
        {
            alertManager.NewAlert("An error has occured: " + e.Message);
        }
        alertManager.NewAlert("Saved to: " + path);
        System.Diagnostics.Process.Start(
    "EXPLORER.EXE", "/select,\"" + path + "\"");
    }
    public void Retake()
    {
        isRunning = false;
        Player.volume = 1;
        ReviewPanel.SetActive(false);
        Sender.ReturnFromReviewing();
    }
}
