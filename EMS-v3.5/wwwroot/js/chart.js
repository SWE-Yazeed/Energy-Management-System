// البيانات لتجميع استهلاك الطاقة حسب المواقع
var stackedLabels = @Html.Raw(JsonConvert.SerializeObject(Model?.Select(e => e.Date.ToString("yyyy-MM-dd")).Distinct().ToList() ?? new List < string > ()));

var stackedData = {
    datasets: [
        {
            label: 'Residential',
            data: @Html.Raw(JsonConvert.SerializeObject(Model?.Where(e => e.BuildingType == "Residential").Select(e => (double)(e.EnergyUsed ?? 0)).ToList() ?? new List < double > ())),
            backgroundColor: 'rgba(75, 192, 192, 0.6)',
            borderColor: 'rgba(75, 192, 192, 1)',
            borderWidth: 1
        },
        {
            label: 'Commercial',
            data: @Html.Raw(JsonConvert.SerializeObject(Model?.Where(e => e.BuildingType == "Commercial").Select(e => (double)(e.EnergyUsed ?? 0)).ToList() ?? new List < double > ())),
            backgroundColor: 'rgba(255, 99, 132, 0.6)',
            borderColor: 'rgba(255, 99, 132, 1)',
            borderWidth: 1
        },
        // أضف المزيد من الفئات (إذا كانت موجودة) بنفس الطريقة
    ]
};

var ctxStacked = document.getElementById('stackedBarChart').getContext('2d');
new Chart(ctxStacked, {
    type: 'bar',
    data: {
        labels: stackedLabels, // التواريخ
        datasets: stackedData.datasets
    },
    options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'top',
            },
        },
        scales: {
            x: {
                stacked: true, // تمكين التكديس على المحور X
            },
            y: {
                stacked: true, // تمكين التكديس على المحور Y
                title: {
                    display: true,
                    text: 'Energy Consumption (KWh)'
                }
            }
        }
    }
});
