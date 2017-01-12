// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Blinky
{
    public sealed partial class MainPage : Page
    {
        private int[] pinNums = { 5, 9, 11, 22, 10, 17, 27}; //{ 1+2, 3, 4, 5, 6, 7, 8 };
        private GpioPin[] pins = new GpioPin[8];
        private GpioPinValue value = GpioPinValue.High;
        private DispatcherTimer timer;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
        private readonly SHT15 _sht15 = new SHT15(24, 23);
        private static int _maximumTemperature = 500;
        private const string I2C_CONTROLLER_NAME = "I2C1";
        private I2cDevice I2CDev;
        private TSL2561 TSL2561Sensor;
        private GpioPin fanPin;

        // TSL Gain and MS Values
        private Boolean Gain = false;
        private uint MS = 0;
        private static double CurrentLux = 0;

        public MainPage()
        {
            InitializeComponent();

            fanPin = GpioController.GetDefault().OpenPin(5);
            fanPin.Write(GpioPinValue.High);
            fanPin.SetDriveMode(GpioPinDriveMode.Output);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(5000);
            timer.Tick += Timer_Tick;



            InitializeI2CDevice();
            //InitGPIO();
            //if (pins != null)
            //{
            //}        
            timer.Start();
        }

        private async void InitializeI2CDevice()
        {
            try
            {
                // Initialize I2C device
                var settings = new I2cConnectionSettings(TSL2561.TSL2561_ADDR);

                settings.BusSpeed = I2cBusSpeed.FastMode;
                settings.SharingMode = I2cSharingMode.Shared;

                string aqs = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);  /* Find the selector string for the I2C bus controller                   */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the I2C bus controller device with our selector string           */

                I2CDev = await I2cDevice.FromIdAsync(dis[0].Id, settings);    /* Create an I2cDevice with our selected bus controller and I2C settings */
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());

                return;
            }

            initializeSensor();
        }

        private void initializeSensor()
        {
            // Initialize Sensor
            TSL2561Sensor = new TSL2561(ref I2CDev);

            // Set the TSL Timing
            MS = (uint)TSL2561Sensor.SetTiming(false, 2);
            // Powerup the TSL sensor
            TSL2561Sensor.PowerUp();

            Debug.WriteLine("TSL2561 ID: " + TSL2561Sensor.GetId());
        }

        private void InitMechanicalSwitch()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pins = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            for (int i = 0; i < pinNums.Length; i++)
            {
                pins[i] = gpio.OpenPin(pinNums[i]);
                pins[i].Write(GpioPinValue.High);
                pins[i].SetDriveMode(GpioPinDriveMode.Output);
            }

            GpioStatus.Text = "GPIO pin initialized correctly.";
        }

        private void Timer_Tick(object sender, object e)
        {
            GetTempHumSensorReadings();

            GetLuminosityReadings();

            value = (value == GpioPinValue.High) ? GpioPinValue.Low : GpioPinValue.High;
            fanPin.Write(value);
            //for (int i = 0; i < pinNums.Length; i++)
            //{
            //    pins[i].Write(value);
            //}
            //LED.Fill = (value == GpioPinValue.High) ? redBrush : grayBrush;
        }

        private void GetLuminosityReadings()
        {
            // Retrive luminosity and update the screen
            uint[] Data = TSL2561Sensor.GetData();

            //Debug.WriteLine("Data1: " + Data[0] + ", Data2: " + Data[1]);

            CurrentLux = TSL2561Sensor.GetLux(Gain, MS, Data[0], Data[1]);

            String strLux = String.Format("{0:0.00}", CurrentLux);
            String strInfo = "Luminosity: " + strLux + " lux";

            Debug.WriteLine(strInfo);

            Luminosity.Text = strInfo;
        }

        /// <summary> 
        /// Get readings from sensors. 
        /// </summary> 
        private void GetTempHumSensorReadings()
        {
            // Get temperature from SHT15 
            // To simulate more realistic engine temperatures, we multiply it 
            var rawTemp = _sht15.ReadRawTemperature();
            var tempF = _sht15.CalculateTemperatureF(rawTemp);
            var tempC = _sht15.CalculateTemperatureC(rawTemp);
            var humidity = _sht15.ReadHumidity(tempC);

            //Temperature.Text = $"Temp: {tempF}";
            //Humidity.Text = $"Humidity: {humidity}";
            Debug.WriteLine($"Temperature: {tempF}");
            Debug.WriteLine($"Humidity: {humidity}");

            // Check if a warning should be generated 
            //var warning = temperature > _maximumTemperature;

        }
    }
}
