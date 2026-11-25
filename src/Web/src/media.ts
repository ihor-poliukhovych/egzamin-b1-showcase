/** URL of an arkusz illustration: task `image` values are relative to /media (e.g. "zadania/2021-01-s5.jpg"). */
export const mediaUrl = (image: string) => `/media/${image.replace(/^\/+/, '')}`
