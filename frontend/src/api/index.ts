/**
 * API模块统一导出
 * 方便使用 import api from '@/api' 的方式导入
 */

import request from './request'
import * as auth from './auth'
import * as chart from './chart'
import * as config from './config'
import * as dataset from './dataset'
import * as datasource from './datasource'
import * as kpi from './kpi'
import * as menu from './menu'
import * as panel from './panel'
import * as report from './report'
import * as ai from './ai'
import * as alert from './alert'

// 默认导出request实例，用于直接调用
export default request

// 具名导出各模块
export {
  auth,
  chart,
  config,
  dataset,
  datasource,
  kpi,
  menu,
  panel,
  report,
  ai,
  alert
}
