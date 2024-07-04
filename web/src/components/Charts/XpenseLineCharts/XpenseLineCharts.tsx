import * as React from "react";
import {LineChart, lineElementClasses} from "@mui/x-charts/LineChart";
import {PieValueType} from "@mui/x-charts";

const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']


export interface XpenseAreaChartEntry {
    date: Date,
    value: number
}

export interface XpenseAreaChartProps {
    data: XpenseAreaChartEntry[],
    hideLegend?: boolean
    height?: number,
    width?: number
}

export default function XpenseAreaChart({data, hideLegend, height, width}: XpenseAreaChartProps) {
    var dates = data.map(e => e.date);
    var values = data.map(e => e.value);

    return (
        <LineChart
            width={500}
            height={300}
            series={[{data: values, label: "uv", area: true, showMark: false}]}
            xAxis={[
                {
                    scaleType: "point",
                    data: dates,
                    valueFormatter: (date) => days[date.getDay()],
                }
            ]}
            sx={{
                [`& .${lineElementClasses.root}`]: {
                    display: "none"
                }
            }}
            slotProps={{
                legend: {
                    hidden: hideLegend ?? true
                }
            }}
        />
    );
}
