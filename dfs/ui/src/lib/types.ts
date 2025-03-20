export interface File {
  id: string
  name: string
  type: "document" | "spreadsheet" | "image" | "other"
  size: number
  folderId: string | null
  createdAt: string
  modifiedAt: string
  thumbnail?: string
}

export interface Folder {
  id: string
  name: string
  parentId: string | null
  createdAt: string
  modifiedAt: string
  hasChildren: boolean
}

