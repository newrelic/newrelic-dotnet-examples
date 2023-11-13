// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace DockerSample
{
    public class WeatherForecast
    {
        public DateOnly Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }
    }
}
