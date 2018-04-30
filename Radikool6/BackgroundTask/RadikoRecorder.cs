﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Radikool6.Classes;
using Radikool6.Entities;
using Radikool6.Models;
using Radikool6.Radio;

namespace Radikool6.BackgroundTask
{
    public class RadikoRecorder : Recorder, IRecorder
    {
        private string _token;
        private Process _ffmpeg;
        private string _filename;
        private Entities.Program _program = new Entities.Program();

        public RadikoRecorder(CommonConfig config, ReserveTask task = null) : base(config, task)
        {
        //    Start();
        }

        public async Task TimeFree(Entities.Program program)
        {
            _program = program;
            Directory.CreateDirectory("records");
            _filename = Path.Combine("records", $"{Guid.NewGuid().ToString()}.aac");
            StartTime = DateTime.Now;
            var m3U8 = await Radiko.GetTimeFreeM3U8(program);
            var arg = $"-i {m3U8} -acodec copy \"{_filename}\"";
            CreateProcess(arg);

        //    await System.Threading.Tasks.Task.Factory.StartNew(() =>
        //    {
                _ffmpeg.Start();

                _ffmpeg.BeginOutputReadLine();
                _ffmpeg.BeginErrorReadLine();
         //   });

        }

        public ReserveTask GetStatus()
        {
            return new ReserveTask(){ Start = StartTime, End = Task.End, Status = (DateTime.Now - StartTime).ToString(@"hh\:mm\:ss")};
        }

        private void CreateProcess(string arg)
        {
            _ffmpeg = new Process
            {
                StartInfo =
                {
                    FileName = "ffmpeg",
                    Arguments = arg,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }
            };

            
            _ffmpeg.OutputDataReceived += process_OutputDataReceived;
            _ffmpeg.ErrorDataReceived += process_OutputDataReceived;
        //    _ffmpeg.Exited += process_Exited;
            
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Info($"ffmpeg起動:{arg}");

            _ffmpeg.Exited += (sender, args) =>
            {
                logger.Info($"タイムフリー録音終了");
            };
        }



        public async Task Start()
        {
            try
            {
                using (var con = new SqliteConnection($"Data Source={Define.File.DbFile}"))
                {
                    var pModel = new ProgramModel(con);
                    _program = pModel.Search(new ProgramSearchCondition() { StationId = Task.Station.Id, From = Task.Reserve.Start, To = Task.Reserve.End}).FirstOrDefault();
                }

                if (Task.Reserve.IsTimeFree)
                {
                    // 番組情報取得
                    using (var con = new SqliteConnection($"Data Source={Define.File.DbFile}"))      
                    {
                        _program.Station = Task.Station;
                        TimeFree(_program);
                    }

                }
                else
                {                   
                    Directory.CreateDirectory("records");
                    _filename = Path.Combine("records", $"{Guid.NewGuid().ToString()}.aac");
                    StartTime = DateTime.Now;
                    var t = Task.End - Task.Start;
                    _token = await Radio.Radiko.GetAuthToken();
                    var arg = Define.Radiko.FfmpegArgs.Replace("[TOKEN]", _token)
                        .Replace("[TIME]", (Task.End - DateTime.Now).ToString(@"hh\:mm\:ss"))
                        .Replace("[CH]", Task.Station.Code)
                        .Replace("[FILE]", _filename);
                    CreateProcess(arg);

                    _ffmpeg.Start();
                    _ffmpeg.BeginOutputReadLine();
                    _ffmpeg.BeginErrorReadLine();

                    var logger = NLog.LogManager.GetCurrentClassLogger();
                    logger.Info($"ffmpeg起動:{arg}");

                    _ffmpeg.Exited += (sender, args) =>
                    {
                        logger.Info($"録音終了");
                        
                    };
                }

            }
            catch (Exception ex)
            {
                var a = ex.Message;
            }
         }


        void process_Exited(object sender, System.EventArgs e)
        {
            using (var con = new SqliteConnection($"Data Source={Define.File.DbFile}"))
            {
                con.Open();
                var lModel = new LibraryModel(con);
                lModel.Update(new Library() { Id = Guid.NewGuid().ToString(), FileName = _filename, Path = _filename, Program = _program });

            }
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                this.process_Exited(sender, e);
            }
            else
            {

                var a = System.Text.Encoding.UTF8.GetBytes(e.Data);
            }

        }


    }
}