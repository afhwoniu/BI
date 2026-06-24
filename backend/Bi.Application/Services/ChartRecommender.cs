namespace Bi.Application.Services;

/// <summary>
/// 图表推荐结果
/// </summary>
public class ChartRecommendation
{
    /// <summary>
    /// 推荐的图表类型
    /// </summary>
    public string ChartType { get; set; } = "bar";
    
    /// <summary>
    /// 推荐理由
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// 图表智能推荐服务
/// </summary>
public class ChartRecommender
{
    /// <summary>
    /// 根据数据特征推荐图表类型
    /// </summary>
    /// <param name="data">查询结果数据</param>
    /// <param name="dimensions">维度字段</param>
    /// <param name="measures">度量字段</param>
    /// <returns>推荐结果</returns>
    public ChartRecommendation Recommend(
        List<Dictionary<string, object?>> data, 
        List<string> dimensions, 
        List<string> measures)
    {
        if (data == null || data.Count == 0)
        {
            return new ChartRecommendation
            {
                ChartType = "table",
                Reason = "数据为空，建议使用表格展示",
                Confidence = 1.0
            };
        }
        
        var rowCount = data.Count;
        var dimCount = dimensions.Count;
        var measureCount = measures.Count;
        
        // 规则1：单个数值，使用指标卡
        if (rowCount == 1 && measureCount == 1 && dimCount == 0)
        {
            return new ChartRecommendation
            {
                ChartType = "indicator",
                Reason = "单个数值适合使用指标卡展示",
                Confidence = 0.95
            };
        }
        
        // 规则2：无维度多度量，使用仪表盘或指标卡组
        if (rowCount == 1 && dimCount == 0 && measureCount > 1)
        {
            return new ChartRecommendation
            {
                ChartType = "indicator",
                Reason = "多个汇总指标适合使用指标卡组展示",
                Confidence = 0.9
            };
        }
        
        // 规则3：单维度，根据数据量选择
        if (dimCount == 1)
        {
            // 检查维度是否为时间类型
            var isTimeDimension = IsTimeDimension(dimensions[0], data);
            
            if (isTimeDimension)
            {
                return new ChartRecommendation
                {
                    ChartType = "line",
                    Reason = "时间序列数据适合使用折线图展示趋势",
                    Confidence = 0.9
                };
            }
            
            // 少量分类（<=7）适合饼图
            if (rowCount <= 7 && measureCount == 1)
            {
                return new ChartRecommendation
                {
                    ChartType = "pie",
                    Reason = "少量分类数据适合使用饼图展示占比",
                    Confidence = 0.85
                };
            }
            
            // 中等数量分类使用柱状图
            if (rowCount <= 20)
            {
                return new ChartRecommendation
                {
                    ChartType = "bar",
                    Reason = "分类数据适合使用柱状图进行对比",
                    Confidence = 0.85
                };
            }
            
            // 大量数据使用表格
            return new ChartRecommendation
            {
                ChartType = "table",
                Reason = "数据量较大，建议使用表格展示",
                Confidence = 0.8
            };
        }
        
        // 规则4：双维度，使用分组柱状图或堆叠图
        if (dimCount == 2)
        {
            return new ChartRecommendation
            {
                ChartType = "bar",
                Reason = "双维度数据适合使用分组柱状图进行对比",
                Confidence = 0.8
            };
        }
        
        // 规则5：多维度，使用表格
        if (dimCount > 2)
        {
            return new ChartRecommendation
            {
                ChartType = "table",
                Reason = "多维度数据适合使用表格展示",
                Confidence = 0.85
            };
        }
        
        // 默认使用柱状图
        return new ChartRecommendation
        {
            ChartType = "bar",
            Reason = "默认推荐柱状图",
            Confidence = 0.7
        };
    }
    
    /// <summary>
    /// 判断是否为时间维度
    /// </summary>
    private static bool IsTimeDimension(string fieldName, List<Dictionary<string, object?>> data)
    {
        // 根据字段名判断
        var lowerName = fieldName.ToLower();
        if (lowerName.Contains("date") || lowerName.Contains("time") || 
            lowerName.Contains("year") || lowerName.Contains("month") ||
            lowerName.Contains("day") || lowerName.Contains("日期") ||
            lowerName.Contains("时间") || lowerName.Contains("年") ||
            lowerName.Contains("月"))
        {
            return true;
        }
        
        // 根据数据类型判断
        if (data.Count > 0 && data[0].TryGetValue(fieldName, out var value))
        {
            if (value is DateTime)
                return true;
            
            // 尝试解析为日期
            if (value is string strValue && DateTime.TryParse(strValue, out _))
                return true;
        }
        
        return false;
    }
}

