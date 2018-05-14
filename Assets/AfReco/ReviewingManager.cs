using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UniRx;
using UniRx.Triggers;
using System;

public class ReviewingManager : MonoBehaviour {
    [SerializeField]
    Slider SeekBar;
    [SerializeField]
    GameObject ReviewPanel;
    [SerializeField]
    AlertManager alertManager;
         
    VideoPlayer vp;
    AudioSource Recorder;
    AudioClip Master;
    double StartTime;
    int EndPosInRecording;

    double LengthTime;
    double EndTime;
    
    int StartPosInMaster = 0;

    bool focusOnSeekBar = false;
    bool seekingByPlaying = false;
    RecordingManager Sender;

    bool isRunning = false;

    bool isFirstTime = true;

    // Use this for initialization
    void Start () {
		
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
    public void StartReviewing(RecordingManager sender, VideoPlayer videoPlayer, AudioSource recorder, AudioClip master, double startTime, int endPos)
    {
        ReviewPanel.SetActive(true);
        Sender = sender;
        vp = videoPlayer;
        Recorder = recorder;
        StartTime = startTime;
        Master = master;
        EndPosInRecording = endPos;
        int samples = Recorder.clip.frequency;
        LengthTime = (float)endPos / samples;
        EndTime = StartTime + LengthTime;
        StartPosInMaster = Master.channels * Mathf.FloorToInt(Master.samples * ((float)startTime / Master.length));
        vp.time = startTime;
        vp.Play();
        Recorder.Play();
        if (isFirstTime)
        {
            ObservableEventTrigger et = SeekBar.gameObject.AddComponent<ObservableEventTrigger>();
            et.OnPointerDownAsObservable().Subscribe(x => { focusOnSeekBar = true; });
            et.OnPointerUpAsObservable().Subscribe(x => { focusOnSeekBar = false; });

            SeekBar.OnValueChangedAsObservable().Where(x => !seekingByPlaying).Throttle(new System.TimeSpan(500)).Subscribe(x =>
            {
                double time = StartTime + x * LengthTime;
                vp.time = time;
                Recorder.time = (float)(x * LengthTime);
                Recorder.Play();
                vp.Play();
                vp.seekCompleted += OnSeekCompleted;
            });
        }
        isFirstTime = false;
        isRunning = true;
    }

    private void OnSeekCompleted(VideoPlayer source)
    {
        Recorder.time = (float)(vp.time - StartTime);
    }

    public void Play()
    {

        vp.Play();
        Recorder.Play();
    }
    public void Pause()
    {
        vp.Pause();
        Recorder.Pause();
    }
    public void Stop()
    {
        vp.Stop();
        Recorder.Stop();
    }
    public void WriteToMaster()
    {
        isRunning = false;
        float[] rec = new float[EndPosInRecording * Recorder.clip.channels];
        Recorder.clip.GetData(rec, 0);
        Master.SetData(rec, StartPosInMaster);
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
        ReviewPanel.SetActive(false);
        Sender.ReturnFromReviewing();
    }
}
