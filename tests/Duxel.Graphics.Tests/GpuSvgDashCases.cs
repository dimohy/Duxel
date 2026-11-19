internal static class GpuSvgDashCases
{
    internal static (string Name, string Content)[] All =>
    [
        ("dash-multicurve-round", "<path d='M5 36C10 5 20 7 28 24L40 8L58 35' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-linejoin='round' stroke-dasharray='21 3' fill='none'/>"),
        ("dash-multicurve-miter", "<path d='M5 36C10 5 20 7 28 24L40 8L58 35' stroke='red' stroke-width='3.2' stroke-linecap='square' stroke-dasharray='21 3' fill='none'/>"),
        ("dash-multicurve-bevel", "<path d='M5 36C10 5 20 7 28 24L40 8L58 35' stroke='red' stroke-width='3.2' stroke-linejoin='bevel' stroke-dasharray='21 3' fill='none'/>"),
        ("dash-square-round-join", "<path d='M10 10L50 40' stroke='red' stroke-width='8' stroke-linecap='square' stroke-linejoin='round' stroke-dasharray='100 4' fill='none'/>"),
        ("dash-closed", "<path d='M10 30C5 2 58 2 54 30C45 47 18 45 10 30Z' stroke='red' stroke-width='3' stroke-linecap='round' stroke-linejoin='round' stroke-dasharray='23 4' fill='none'/>"),
        ("dash-closed-covered", "<path d='M8 8L52 8L50 38L10 35Z' stroke='red' stroke-width='3.2' stroke-linecap='square' stroke-dasharray='300 4' fill='none'/>"),
        ("dash-closed-boundary", "<path d='M8 8L52 8L50 38L10 35Z' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='23 4 10 3' stroke-dashoffset='27' fill='none'/>"),
        ("dash-calibrated", "<path d='M4 36C15 -3 35 57 60 10' pathLength='100' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='5 7' stroke-dashoffset='-3' fill='none'/>"),
        ("dash-multicontour", "<path d='M4 15C10 -2 29 42 60 8M5 36Q25 10 58 38' pathLength='200' stroke='red' stroke-width='2.2' stroke-linecap='round' stroke-dasharray='9 11' fill='none'/>"),
        ("dash-zero-round", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='0 7' fill='none'/>"),
        ("dash-zero-square", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' stroke-linecap='square' stroke-dasharray='0 7' fill='none'/>"),
        ("dash-odd-offset", "<path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-dasharray='3 2 5' stroke-dashoffset='-13.25' fill='none'/>"),
        ("dash-reflected", "<g transform='translate(62 2) scale(-.9 .83)'><path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='4 3' fill='none'/></g>"),
        ("dash-arc", "<path d='M8 30A22 15 25 1 1 53 25' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='4 3' fill='none'/>"),
        ("dash-cusp", "<path d='M8 24C60 24 -15 24 54 24' stroke='red' stroke-width='3.2' stroke-linecap='round' stroke-dasharray='4 3' fill='none'/>")
    ];
}
