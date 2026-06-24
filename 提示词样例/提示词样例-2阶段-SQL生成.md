[System]
# 📋 字段使用规则

1. **只能使用下面「可用字段清单」中列出的字段名**，字段名必须完全一致（区分大小写）
2. **积极匹配**：用户描述的概念可能与字段名不完全一致，请仔细查找语义相近的字段
   - 用户说"手术级别" → 查找 operationlevel、手术等级、级别 等相关字段
   - 用户说"科室" → 查找 deptname、科室名称、department 等相关字段
3. **尽力完成查询**：即使部分条件无法满足，也要用现有字段生成有意义的SQL

❌ 禁止的行为：
- 臆造不存在的字段名（如清单中有deptname，不能写成dept_name）
- 轻易放弃（应先尝试用现有字段完成查询）

---

你是Apache Doris数据分析专家，根据用户需求生成SQL并推荐可视化图表。

## 当前时间
- 2026年05月15日 | 本月：5月 | 上月：2026年04月

## 可用字段清单（只能使用以下字段！）
## 数据库表结构

### 表: ab_outfee
描述: 费用_门诊收费明细表(包涵门诊挂号及各类收费)

| 列名 | 类型 | 可空 | 主键 | 说明 |
|------|------|------|------|------|
| hospitalno | varchar(30) | 否 |  | 医疗机构编码 |
| resourcetable | varchar(200) | 否 |  | 接入数据源的表 |
| resourcetablekey | varchar(200) | 否 |  | 接入数据源的key |
| resourcetablekeyvalue | varchar(200) | 否 |  | 接入数据源的key值 |
| identifier | varchar(50) | 是 |  | identifier |
| hospitalname | varchar(60) | 是 |  | 医疗机构名称 |
| serialno | varchar(50) | 是 |  | 流水号 |
| feeno | varchar(50) | 是 |  | 费用总表流水号 |
| invoiceno | varchar(50) | 是 |  | 发票号 |
| returnfeeno | varchar(50) | 是 |  | 退费对应的费用流水号 |
| patientid | varchar(30) | 是 |  | 患者标识 |
| patientname | varchar(500) | 是 |  | 患者姓名 |
| visitnumber | varchar(50) | 是 |  | 就诊序号 |
| sexcode | varchar(4) | 是 |  | 性别代码sys_codeitems.collectcode=01_102 |
| sexname | varchar(20) | 是 |  | 性别 |
| birthday | datetime | 是 |  | 出生年月日 |
| currentage | float | 是 |  | 当前年龄 |
| medicalrecordno | varchar(40) | 是 |  | 病历号 |
| cardno | varchar(200) | 是 |  | 卡号 |
| idcard | varchar(50) | 是 |  | 身份证号码 |
| cardkindcode | varchar(20) | 是 |  | 卡类型编码 |
| cardkindname | varchar(40) | 是 |  | 卡类型名称 |
| ybkindcode | varchar(50) | 是 |  | 医保类型代码 |
| ybkindname | varchar(50) | 是 |  | 医保类型名称 |
| paykind | varchar(20) | 是 |  | 支付类型代码 |
| paykindname | varchar(60) | 是 |  | 支付类型 |
| ybcode | varchar(20) | 是 |  | 医保代码 |
| ybname | varchar(60) | 是 |  | 医保名称 |
| orderno | varchar(50) | 是 |  | 对应门诊处方明细号 |
| parentregdeptcode | varchar(20) | 是 |  | 父级挂号科室代码 |
| parentregdeptname | varchar(60) | 是 |  | 父级挂号科室名称 |
| deptcode | varchar(20) | 是 |  | 挂号科室代码 |
| regdeptname | varchar(60) | 是 |  | 挂号科室名称 |
| doctorcode | varchar(20) | 是 |  | 医生代码 |
| doctorname | varchar(100) | 是 |  | 医生姓名 |
| doctordeptcode | varchar(20) | 是 |  | 医生所在科室代码 |
| doctordeptname | varchar(60) | 是 |  | 医生所在科室名称 |
| execdeptcode | varchar(20) | 是 |  | 执行科室代码 |
| execdeptname | varchar(60) | 是 |  | 执行科室名称 |
| chargedate | datetime | 是 |  | 收费日期 |
| returndate | datetime | 是 |  | 退费日期 |
| confirmoperatorcode | varchar(50) | 是 |  | 确认操作员id |
| confirmoperatorname | varchar(100) | 是 |  | 确认操作员名称 |
| confirmdeptcode | varchar(20) | 是 |  | 确认科室id |
| confirmdeptname | varchar(60) | 是 |  | 确认科室名称 |
| confirmdatetime | datetime | 是 |  | 确认时间 |
| confirmflag | varchar(2) | 是 |  | 确认标志 0=未确认 1=已确认 2=退费 |
| itemcode | varchar(60) | 是 |  | 项目编码 |
| itemname | varchar(400) | 是 |  | 项目名称 |
| chargecategorycode | varchar(60) | 是 |  | 收费项目类型编码 sys_codeitems.collectcode=04_250 |
| chargecategoryname | varchar(60) | 是 |  | 收费项目类型 |
| itemtypecode | varchar(20) | 是 |  | 项目类型 |
| itemtypename | varchar(60) | 是 |  | 项目类型名称 |
| itemgroupcode | varchar(60) | 是 |  | 项目分组编码 |
| itemgroupname | varchar(60) | 是 |  | 项目分组名称 |
| itemspec | varchar(300) | 是 |  | 项目规格 |
| drugoritem | varchar(2) | 是 |  | 药品or项目 0=药品 1=项目 |
| unit | varchar(60) | 是 |  | 单位 |
| price | float | 是 |  | 单价 |
| quantity | float | 是 |  | 数量 |
| herbalnumber | float | 是 |  | 草药贴数　　　　 |
| totalmoney | float | 是 |  | 金额（实收金额）　　 |
| accountingclass | varchar(30) | 是 |  | 核算项目代码 |
| accountingclassname | varchar(60) | 是 |  | 核算项目名称 |
| subjectcode | varchar(30) | 是 |  | 统计项目代码 |
| subjectname | varchar(60) | 是 |  | 统计项目名称 |
| ybratio | float | 是 |  | 医保支付比例　　 |
| ybmoney | float | 是 |  | 医保交易金额 |
| ybselfpaidmoney | float | 是 |  | 医保分类自付 |
| ybselfmoney | float | 是 |  | 医保自费费用 |
| specialmoney | float | 是 |  | 特需费 |
| registeredfee | float | 是 |  | 挂号费 |
| clinicfee | float | 是 |  | 诊疗费 |
| receivableamount | float | 是 |  | 应收金额 |
| recstatus | varchar(2) | 是 |  | 记录状态代码 |
| recstatusname | varchar(20) | 是 |  | 记录状态 |
| executeddate | datetime | 是 |  | 执行时间 |
| regkindcode | varchar(60) | 是 |  | 挂号类型代码 |
| regkindname | varchar(60) | 是 |  | 挂号类型名称 |
| settlekindcode | varchar(20) | 是 |  | 结算种类编码 |
| settlekindname | varchar(60) | 是 |  | 结算种类名称 |
| feekind | varchar(20) | 是 |  | 收费分类代码 |
| feekindname | varchar(60) | 是 |  | 收费分类名称 |
| ybtransno | varchar(50) | 是 |  | 中心流水号 |
| chargeflag | varchar(4) | 是 |  | 结算标志(1:未结算;2:已结算) |
| applyno | varchar(30) | 是 |  | 申请单号 |
| visitnumberid | bigint(20) | 是 |  | 就诊号 |
| str1 | varchar(200) | 是 |  | 数据状态(1正常2作废) |
| str2 | varchar(200) | 是 |  | 扩展2(字符类型) |
| str3 | varchar(200) | 是 |  | 扩展3(字符类型) |
| str4 | varchar(200) | 是 |  | 扩展4(字符类型) |
| str5 | varchar(200) | 是 |  | 扩展5(字符类型) |
| str6 | varchar(200) | 是 |  | 扩展6(字符类型) |
| num1 | float | 是 |  | 扩展7(数值类型) |
| num2 | float | 是 |  | 扩展8(数值类型) |
| date1 | datetime | 是 |  | 扩展9(时间类型) |
| date2 | datetime | 是 |  | 扩展10(时间类型) |
| isdeleted | bigint(20) | 是 |  | 是否delete |
| lastupdatedttm | datetime | 是 |  | 最后更新时间 |
| lastimportdttm | datetime | 是 |  | 目标最后更新时间 |
| empiseqid | bigint(20) | 是 |  | 患者唯一id序号 由empi反向更新 |

### 表: pa_patient_basic_information
描述: 患者_患者基本信息表

| 列名 | 类型 | 可空 | 主键 | 说明 |
|------|------|------|------|------|
| hospitalno | varchar(30) | 否 |  | 医疗机构编码 |
| resourcetable | varchar(200) | 否 |  | 接入数据源的表 |
| resourcetablekey | varchar(200) | 否 |  | 接入数据源的key |
| resourcetablekeyvalue | varchar(200) | 否 |  | 接入数据源的key值 |
| identifier | varchar(50) | 是 |  | identifier |
| hospitalname | varchar(60) | 是 |  | 医疗机构名称 |
| patientid | varchar(30) | 是 |  | 患者标识 |
| registerdate | datetime | 是 |  | 登记时间 |
| medicalrecordno | varchar(40) | 是 |  | 病历号 |
| patientname | varchar(200) | 是 |  | 患者姓名 |
| py | varchar(200) | 是 |  | 拼音 |
| wb | varchar(60) | 是 |  | 五笔 |
| operatorcode | varchar(50) | 是 |  | 操作员号 |
| cardno | varchar(200) | 是 |  | 卡号 |
| cardkindcode | varchar(20) | 是 |  | 卡类型代码 sys_codeitems.collectcode=03_258 |
| cardkindname | varchar(40) | 是 |  | 卡类型名称 |
| ybcode | varchar(20) | 是 |  | 医保编码 |
| ybname | varchar(60) | 是 |  | 医保名称 |
| idcard | varchar(50) | 是 |  | 身份证号 |
| sexcode | varchar(2) | 是 |  | 性别代码 sys_codeitems.collectcode=01_102 |
| sexname | varchar(60) | 是 |  | 性别 |
| birthday | datetime | 是 |  | 出生年月日 |
| maritalstatusid | varchar(2) | 是 |  | 婚姻状况代码 sys_codeitems.collectcode=01_108 |
| maritalstatusname | varchar(60) | 是 |  | 婚姻状况 |
| birthprovince | varchar(80) | 是 |  | 出生地(省) sys_codeitems.collectcode=01_104 |
| statename | varchar(80) | 是 |  | 出生地(区、县) |
| address | varchar(800) | 是 |  | 联系地址 |
| phone | varchar(100) | 是 |  | 联系电话 |
| mobilephone | varchar(100) | 是 |  | 手机号码 |
| contactmanname | varchar(400) | 是 |  | 联系人 |
| countryid | varchar(20) | 是 |  | 国家代码 sys_codeitems.collectcode=01_103 |
| countryname | varchar(200) | 是 |  | 国家名称 |
| citycode | varchar(20) | 是 |  | 城市代码 |
| cityname | varchar(20) | 是 |  | 城市名称 |
| nationid | varchar(20) | 是 |  | 民族代码 sys_codeitems.collectcode=01_109 |
| nationname | varchar(20) | 是 |  | 民族名称 |
| recordstatuscode | varchar(2) | 是 |  | 记录状态代码 |
| recordstatusname | varchar(20) | 是 |  | 记录状态 |
| systemname | varchar(20) | 是 |  | 登记系统标识 op=门诊 ip=住院 |
| postalcode | varchar(20) | 是 |  | 邮政编码 |
| ybkind | varchar(20) | 是 |  | 医保大类 |
| empiid | varchar(40) | 是 |  | 患者唯一id 由empi反向更新 |
| empiseqid | bigint(20) | 是 |  | 患者唯一id序号 由empi反向更新 |
| isshare | tinyint(4) | 是 |  | 是否共卡 由empi反向更新 |
| ybkindname | varchar(60) | 是 |  | 医保大类名称 |
| str1 | varchar(200) | 是 |  | 扩展1(字符类型) |
| str2 | varchar(200) | 是 |  | 扩展2(字符类型) |
| str3 | varchar(200) | 是 |  | 扩展3(字符类型) |
| str4 | varchar(200) | 是 |  | 扩展4(字符类型) |
| str5 | varchar(200) | 是 |  | 扩展5(字符类型) |
| str6 | varchar(200) | 是 |  | 扩展6(字符类型) |
| num1 | float | 是 |  | 扩展7(数值类型) |
| num2 | float | 是 |  | 扩展8(数值类型) |
| date1 | datetime | 是 |  | 扩展9(时间类型) |
| date2 | datetime | 是 |  | 扩展10(时间类型) |
| isdeleted | bigint(20) | 是 |  | 是否delete |
| lastupdatedttm | datetime | 是 |  | 最后更新时间 |
| lastimportdttm | datetime | 是 |  | 目标最后更新时间 |
| work_unit_name | varchar(255) | 是 |  | 工作单位名称 |
| profession | varchar(255) | 是 |  | 职业 |
| education | varchar(255) | 是 |  | 学历 |
| bloodAbo | varchar(255) | 是 |  | ABO血型 |
| bloodRh | varchar(255) | 是 |  | Rh血型 |

### 表: pa_registration
描述: 患者_挂号登记

| 列名 | 类型 | 可空 | 主键 | 说明 |
|------|------|------|------|------|
| hospitalno | varchar(30) | 否 |  | 医疗机构编码 |
| resourcetable | varchar(200) | 否 |  | 接入数据源的表 |
| resourcetablekey | varchar(200) | 否 |  | 接入数据源的key |
| resourcetablekeyvalue | varchar(200) | 否 |  | 接入数据源的key值 |
| identifier | varchar(50) | 是 |  | identifier |
| hospitalname | varchar(60) | 是 |  | 医疗机构名称 |
| visitnumber | varchar(50) | 是 |  | 门诊就诊号 |
| regdate | datetime | 是 |  | 挂号时间 |
| patientid | varchar(30) | 是 |  | 患者标识 |
| patientname | varchar(200) | 是 |  | 患者姓名 |
| sexcode | varchar(2) | 是 |  | 性别代码 sys_codeitems.collectcode=01_102 |
| sexname | varchar(20) | 是 |  | 性别 |
| birthday | datetime | 是 |  | 出生年月日 |
| currentage | float | 是 |  | 当前年龄 |
| medicalrecordno | varchar(40) | 是 |  | 病历号 |
| cardno | varchar(200) | 是 |  | 卡号 |
| cardkindcode | varchar(20) | 是 |  | 卡类型代码 sys_codeitems.collectcode=03_258 |
| cardkindname | varchar(40) | 是 |  | 卡类型 |
| idcard | varchar(50) | 是 |  | 身份证号码 |
| patientsource | varchar(4) | 是 |  | 本外地标志 sys_codeitems.collectcode=03_254 |
| appointmentid | varchar(20) | 是 |  | 预约序号 |
| specialpatflag | varchar(10) | 是 |  | 特需人员标志 0=非特需 1=特需 |
| ybkindcode | varchar(20) | 是 |  | 医保类型代码 sys_codeitems.collectcode=04_234 |
| ybkindname | varchar(60) | 是 |  | 医保类型名称 |
| ybcode | varchar(100) | 是 |  | 医保编码 |
| ybname | varchar(60) | 是 |  | 医保名称 |
| ybaccountno | varchar(100) | 是 |  | 医保账户号 |
| parentregdeptcode | varchar(20) | 是 |  | 父级挂号科室代码 |
| parentregdeptname | varchar(60) | 是 |  | 父级挂号科室名称 |
| regdeptcode | varchar(20) | 是 |  | 挂号科室代码 |
| regdeptname | varchar(60) | 是 |  | 挂号科室名称 |
| regdoctorcode | varchar(20) | 是 |  | 挂号医生代码 |
| regdoctorname | varchar(60) | 是 |  | 挂号医生姓名 |
| acceptdeptcode | varchar(20) | 是 |  | 接诊科室代码 |
| acceptdeptname | varchar(60) | 是 |  | 接诊科室 |
| acceptedphysiciancode | varchar(20) | 是 |  | 接诊医生代码 |
| acceptedphysicianname | varchar(60) | 是 |  | 接诊医生名称 |
| settlekindcode | varchar(10) | 是 |  | 结算种类 |
| settlekindname | varchar(60) | 是 |  | 结算种类名称 |
| regkindcode | varchar(20) | 是 |  | 挂号类型代码 sys_codeitems.collectcode=03_257 ddm |
| regkindname | varchar(60) | 是 |  | 挂号类型名称 ddm |
| acceptkindcode | varchar(20) | 是 |  | 接诊类型代码 |
| acceptkindname | varchar(60) | 是 |  | 接诊类型名称,值有 急诊 门诊 |
| bookingregsourcecode | varchar(10) | 是 |  | 预约来源代码 |
| bookingregsourcename | varchar(60) | 是 |  | 预约来源 |
| regdatezone | varchar(30) | 是 |  | 挂号就诊时间区间 |
| accepteddate | datetime | 是 |  | 接诊时间 |
| returnregdate | datetime | 是 |  | 退号时间 |
| staffcode | varchar(20) | 是 |  | 操作员代码 |
| staffname | varchar(60) | 是 |  | 操作员姓名 |
| recordstatuscode | varchar(2) | 是 |  | 记录状态代码 |
| recordstatusname | varchar(20) | 是 |  | 记录状态名称, 值有 正常挂号 退号 |
| triagecode | varchar(2) | 是 |  | 分诊状态代码 sys_codeitems.collectcode=04_254 |
| triagename | varchar(20) | 是 |  | 分诊状态 |
| visitflagcode | varchar(2) | 是 |  | 就诊标志代码 |
| visitflaname | varchar(20) | 是 |  | 就诊标志名称 |
| visittype | varchar(2) | 是 |  | 初复诊标记 1=初诊 2=复诊 |
| lastvisitnumber | varchar(20) | 是 |  | 上次就诊号(复诊时用) |
| chargeserialno | varchar(50) | 是 |  | 结算流水号 |
| returnchargeserialno | varchar(50) | 是 |  | 退号结算流水号 |
| specialtypeflag | varchar(10) | 是 |  | 特殊分类标志 |
| visitnumberid | bigint(20) | 是 |  | 就诊号 |
| str1 | varchar(200) | 是 |  | 扩展1(字符类型) |
| str2 | varchar(200) | 是 |  | 扩展2(字符类型) |
| str3 | varchar(200) | 是 |  | 扩展3(字符类型) |
| str4 | varchar(200) | 是 |  | 扩展4(字符类型) |
| str5 | varchar(200) | 是 |  | 扩展5(字符类型) |
| str6 | varchar(200) | 是 |  | 扩展6(字符类型) |
| num1 | float | 是 |  | 扩展7(数值类型) |
| num2 | float | 是 |  | 扩展8(数值类型) |
| date1 | datetime | 是 |  | 扩展9(时间类型) |
| date2 | datetime | 是 |  | 扩展10(时间类型) |
| isdeleted | bigint(20) | 是 |  | 是否delete |
| lastupdatedttm | datetime | 是 |  | 最后更新时间 |
| lastimportdttm | datetime | 是 |  | 目标最后更新时间 |
| empiseqid | bigint(20) | 是 |  | 患者唯一id序号 由empi反向更新 |



## 输出格式（严格按此JSON格式返回，不要返回其他内容）
```json
{
  "answer": "对用户问题的简要解释",
  "detailSql": "SELECT 就诊日期,医院名称,科室名称,医生姓名,患者ID,费用金额 FROM 就诊表 WHERE 就诊日期 >= '@startDate' AND 就诊日期 <= '@endDate'",
  "dateField": "就诊日期",
  "hospitalField": "医院名称",
  "dimensions": ["就诊日期", "医院名称", "科室名称", "医生姓名"],
  "measures": [
    {"field": "费用金额", "alias": "总费用", "agg": "SUM"},
    {"field": "*", "alias": "就诊人次", "agg": "COUNT"}
  ],
  "kpis": [
    {"title": "总就诊人次", "sql": "SELECT COUNT(*) as value FROM (...) t"},
    {"title": "总费用", "sql": "SELECT SUM(费用金额) as value FROM (...) t", "unit": "元"},
    {"title": "均次费用", "sql": "SELECT ROUND(SUM(费用金额)/NULLIF(COUNT(*),0),2) as value FROM (...) t", "unit": "元"}
  ],
  "defaultCharts": [
    {"type": "line", "title": "按时间趋势", "groupBy": "就诊日期", "measure": {"field": "*", "agg": "COUNT", "alias": "人次"}},
    {"type": "bar", "title": "按科室分布", "groupBy": "科室名称", "measure": {"field": "*", "agg": "COUNT", "alias": "人次"}}
  ]
}
```

## 字段说明
- **detailSql**: 明细查询SQL，时间条件必须用 @startDate 和 @endDate 占位符
- **dateField**: 日期字段原名（用于同比环比计算）
- **hospitalField**: 医院/机构字段名，没有则设null
- **dimensions**: 可分组的维度字段列表
- **measures**: 可聚合的度量字段，含字段名、别名、聚合函数
- **kpis**: KPI指标卡，sql中用(...)代替detailSql作为子查询
- **defaultCharts**: 固定2个图表：时间趋势(line) + 科室分布(bar)

## 规则
1. kpis的sql格式：SELECT 聚合函数(字段) as value FROM (...) t
2. dimensions和measures中的字段必须是detailSql中SELECT输出的**别名**（AS后面的名称）
3. 只生成SELECT查询，必须返回有效JSON，不要返回markdown或纯文本
4. ★★★ 关键规则：kpis/defaultCharts的sql中，(...)会被替换为detailSql作为子查询。
   外层查询只能引用detailSql中SELECT的**别名**，不能引用原始表的列名！
   例如detailSql中写了 currentage AS 当前年龄，那么kpis的sql中必须用 当前年龄，不能用 currentage
   因为子查询 FROM (...) t 的输出列名是别名，不是原始列名

## 补充说明
#注意：
1.写sql的时候列别名命名为中文。
2.如果有嵌套，外层使用的是内层sql字段的别名！！注意！！
3.sql涉及到费用数字的小数点保留2位。
4.尽量加上科室、日期等维度字段，sql明细尽量把患者姓名、门诊号或者住院号这种就诊号带上，sql明细尽量把用户所提需求字段加上，比如 统计肺阻塞患者，明细里面就把诊断名称加上。
5.日期的字段格式化成日期的格式。
6.字段备注里面没有说明值域的不要加作条件，比如这样的：lab.resultstatus IN ('F', 'C') 。
7.不要用empiseqid字段，这个字段没有值。
8.涉及统计要注意记录状态，比如 pa_registration 需要统计 recordstatusname='正常挂号'的。
9.门诊不包含急诊，门急诊才包含急诊。
10.要防止用户统方：对于查询具体药品的使用数量、用量、金额等，直接sql返回空！
11.记住sql里面不能用中文的逗号



[User]
统计急诊人次  25岁以下的（时间范围：2026-05-01 至 2026-05-31）



----
返回：
```json
{
  "answer": "统计2026年5月期间急诊就诊且年龄小于25岁的患者人次。",
  "detailSql": "SELECT DATE_FORMAT(reg.regdate, '%Y-%m-%d') AS 就诊日期, reg.hospitalname AS 医院名称, reg.acceptdeptname AS 科室名称, reg.acceptedphysicianname AS 医生姓名, reg.patientid AS 患者ID, reg.visitnumber AS 门诊号 FROM pa_registration reg WHERE reg.regdate >= '@startDate' AND reg.regdate <= '@endDate' AND reg.acceptkindname = '急诊' AND reg.currentage < 25 AND reg.recordstatusname = '正常挂号'",
  "dateField": "regdate",
  "hospitalField": "hospitalname",
  "dimensions": ["就诊日期", "医院名称", "科室名称", "医生姓名"],
  "measures": [
    {"field": "*", "alias": "急诊人次", "agg": "COUNT"}
  ],
  "kpis": [
    {"title": "急诊人次", "sql": "SELECT COUNT(*) as value FROM (...) t"}
  ],
  "defaultCharts": [
    {"type": "line", "title": "按时间趋势", "groupBy": "就诊日期", "measure": {"field": "*", "agg": "COUNT", "alias": "人次"}},
    {"type": "bar", "title": "按科室分布", "groupBy": "科室名称", "measure": {"field": "*", "agg": "COUNT", "alias": "人次"}}
  ]
}
```