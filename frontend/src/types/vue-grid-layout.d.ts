declare module 'vue-grid-layout' {
  import { DefineComponent } from 'vue'

  export interface LayoutItem {
    i: string
    x: number
    y: number
    w: number
    h: number
    static?: boolean
    minW?: number
    maxW?: number
    minH?: number
    maxH?: number
    moved?: boolean
  }

  export const GridLayout: DefineComponent<{
    layout: LayoutItem[]
    colNum?: number
    rowHeight?: number
    maxRows?: number
    margin?: [number, number]
    isDraggable?: boolean
    isResizable?: boolean
    isMirrored?: boolean
    verticalCompact?: boolean
    preventCollision?: boolean
    useCssTransforms?: boolean
    responsive?: boolean
    responsiveLayouts?: Record<string, LayoutItem[]>
    breakpoints?: Record<string, number>
    cols?: Record<string, number>
    showCloseButton?: boolean
  }, {
    'layout-updated': (layout: LayoutItem[]) => void
    'layout-ready': (layout: LayoutItem[]) => void
    'layout-created': (layout: LayoutItem[]) => void
  }>

  export const GridItem: DefineComponent<{
    i: string
    x: number
    y: number
    w: number
    h: number
    static?: boolean
    minW?: number
    maxW?: number
    minH?: number
    maxH?: number
    dragAllowFrom?: string
    dragIgnoreFrom?: string
    resizeIgnoreFrom?: string
    preserveAspectRatio?: boolean
  }, {
    move: () => void
    resize: () => void
  }>
}
